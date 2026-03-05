public class StetsonGetFIFPdf {
    public String lenderType { get; set; }
    public String pfaUUID { get; set; }
    public String uspfURL { get; set; }

    public Opportunity opportunity { get; set; }

    private class GoogleAuthResponse {
        public String id_token;
    }

    private class GetPdfUrlResponse {
        public String signedUrl;
    }

    public PageReference determineLender() {
        if (lenderType == 'USPF') {
            System.debug('USPF');
            return this.getUSPFPdf();
        } else {
            System.debug('FIF');
            return this.getFIFPdf();
        }
    }

    private String callGeneratePdfUrlInGCP(String token, String functionEndpoint, String uuid) {
        GetPdfUrlResponse result = new GetPdfUrlResponse();
        HTTP http = new HTTP();
        HTTPRequest req = new HTTPRequest();
        req.setHeader('Content-Type', 'application/json');
        req.setHeader('Authorization', 'Bearer ' + token);
        String endpoint = functionEndpoint + '?docId=' + uuid;
        req.setEndpoint(endpoint);
        req.setMethod('GET');
        HTTPResponse response = new HttpResponse();
        System.debug('GET request to ' + endpoint);
        if (Test.IsRunningTest()) {
            return 'pdf-url';
        } else {
            response = http.send(req);

            if (response.getStatusCode() == 200) {
                System.debug(endpoint + ' called successfully');
                result = (GetPdfUrlResponse) JSON.deserialize(response.getBody(), GetPdfUrlResponse.class);
                return result.signedUrl;
            } else {
                String errorMsg =
                    'Error occured ' +
                    response.getBody() +
                    ' ' +
                    response.getStatus() +
                    ' ' +
                    response.getStatusCode();
                System.debug(LoggingLevel.ERROR, errorMsg);
                return '';
            }
        }
    }

    private Stetson_Sync_GCP_Key__mdt getGCPServiceAccount() {
        List<Stetson_Sync_GCP_Key__mdt> gcp_sa_keys = new List<Stetson_Sync_GCP_Key__mdt>();
        gcp_sa_keys = [
            SELECT client_email__c, audience__c, token_endpoint__c
            FROM Stetson_Sync_GCP_Key__mdt
            WHERE DeveloperName LIKE '%GCP%'
        ];

        if (gcp_sa_keys.size() > 0) {
            return gcp_sa_keys[0];
        } else {
            throw new GCPServiceAccountException('Cannot find GCP Service Account Keys');
        }
    }

    private GoogleAuthResponse getGCPAuthToken(Stetson_Sync_GCP_Key__mdt serviceAccount) {
        GoogleAuthResponse result = new GoogleAuthResponse();

        Auth.JWT jwt = new Auth.JWT();
        jwt.setAud(serviceAccount.token_endpoint__c);
        jwt.setIss(serviceAccount.client_email__c);

        // Additional claims to set scope
        Map<String, Object> claims = new Map<String, Object>();
        claims.put('target_audience', serviceAccount.audience__c);
        jwt.setAdditionalClaims(claims);

        // Create the object that signs the JWT bearer token
        Auth.JWS jws = new Auth.JWS(jwt, 'gcp_stetson_sync_prod');

        // Get the resulting JWS in case debugging is required
        String token = jws.getCompactSerialization();

        // Set the token endpoint that the JWT bearer token is posted to
        String tokenEndpoint = serviceAccount.token_endpoint__c;

        Auth.JWTBearerTokenExchange bearer = new Auth.JWTBearerTokenExchange(tokenEndpoint, jws);

        if (Test.IsRunningTest()) {
            result.id_token = '123456';
        } else {
            System.HttpResponse response = bearer.getHTTPResponse();

            result = (GoogleAuthResponse) JSON.deserialize(response.getBody(), GoogleAuthResponse.class);
        }
        return result;
    }

    private class GCPServiceAccountException extends Exception {
    }

    public StetsonGetFIFPdf(ApexPages.StandardController controller) {
        opportunity = (Opportunity) controller.getRecord();
        lenderType = opportunity.API_Partner_ID__c;
        pfaUUID = opportunity.FIF_PFA_UUID__c;
        uspfURL = opportunity.Finance_Agreement_PDF__c;
        System.debug('pfaUUID: ' + pfaUUID);
    }

    public PageReference getUSPFPdf() {
        System.debug('uspfURL: ' + uspfURL);
        if (this.uspfURL == '') {
            return new PageReference('javascript:window.close()');
        } else {
            PageReference pageRef = new PageReference(uspfURL);
            pageRef.setRedirect(true);
            return pageRef;
        }
    }

    public PageReference getFIFPdf() {
        System.debug('in getFIFPdf');
        if (this.pfaUUID == '') {
            return new PageReference('javascript:window.close()');
        }

        Stetson_Sync_GCP_Key__mdt serviceAccount = this.getGCPServiceAccount();
        GoogleAuthResponse authResponse = this.getGCPAuthToken(serviceAccount);

        String signedUrl = '';

        if (authResponse.id_token != null) {
            signedUrl = this.callGeneratePdfUrlInGCP(authResponse.id_token, serviceAccount.audience__c, this.pfaUUID);
            if (signedUrl != '') {
                System.debug('Retrieved signed URL: ' + signedUrl);
                PageReference pageRef = new PageReference(signedUrl);
                pageRef.setRedirect(true);
                return pageRef;
            } else {
                return new PageReference('javascript:window.close()');
            }
        } else {
            return new PageReference('javascript:window.close()');
        }
    }
}