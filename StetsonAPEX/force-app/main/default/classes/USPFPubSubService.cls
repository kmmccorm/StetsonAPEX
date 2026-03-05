public without sharing class USPFPubSubService implements Queueable, Database.AllowsCallouts {
    // Serialised message
    private List<USPFPubSubMessage> messages;

    class GoogleAuthResponse {
        public String access_token;
    }

    public USPFPubSubService(List<USPFPubSubMessage> messages) {
        this.messages = messages;
    }
    
    private USPF_PubSub_Setting__mdt  getUSPFServiceAccount() {
        List<USPF_PubSub_Setting__mdt > uspfKeys = new List<USPF_PubSub_Setting__mdt >();
        if(!Test.isRunningTest()) {
            uspfKeys = [SELECT
                        Client_Email__c,
                        Scope__c,
                        Token_Endpoint__c,
                        Function_Endpoint__c
                        FROM USPF_PubSub_Setting__mdt];
        } else {
            uspfKeys.add(new USPF_PubSub_Setting__mdt(
                DeveloperName = 'TestMetadata',
                Client_Email__c = 'test@test.com',
                Scope__c = 'scope',
                Token_Endpoint__c = 'endpoint',
                Function_Endpoint__c = 'function'
            ));
        }


        if (uspfKeys.size() > 0) {
            return uspfKeys[0];
        } else {
            throw new USPFPubSubException('Cannot find USPF Service Account Keys');
        }
    }

    private void sendMessage(String token, String functionEndpoint) {
        // Chunk outgoing messages into batches of 1000, making a separate callout per batch
        if ( messages == null || messages.isEmpty() ) {
            System.debug( 'No messages to send.' );
            return;
        }

        Integer batchSize = 1000;
        Integer total = messages.size();
        Integer batches = ( Integer ) Math.ceil( ( Decimal ) total / batchSize );

        System.debug( 'Preparing to send ' + total + ' messages in ' + batches + ' batch(es) of up to ' + batchSize + '.' );

        for ( Integer i = 0; i < total; i += batchSize ) {
            Integer endIdx = Math.min(i + batchSize, total);
            List<USPFPubSubMessage> batch = new List<USPFPubSubMessage>();
            for ( Integer j = i; j < endIdx; j++ ) {
                batch.add( messages[j] );
            }

            HTTP http = new HTTP();
            HTTPRequest req = new HTTPRequest();
            req.setHeader('Content-Type', 'application/json');
            req.setHeader('Authorization', 'Bearer ' + token);
            req.setEndpoint(functionEndpoint);
            req.setMethod('POST');

            // Build request body for just this batch
            JSONGenerator gen = JSON.createGenerator(false);
            gen.writeStartObject();
            gen.writeFieldName('messages');
            gen.writeStartArray();
            for (USPFPubSubMessage mes : batch) {
                gen.writeStartObject();
                gen.writeFieldName('attributes');
                gen.writeStartObject();
                gen.writeStringField('Content-Type', 'application/json');
                gen.writeEndObject();
                gen.writeStringField('data', EncodingUtil.base64Encode(Blob.valueOf(JSON.serialize(mes))));
                gen.writeEndObject();
            }
            gen.writeEndArray();
            gen.writeEndObject();

            req.setBody(gen.getAsString());
            System.debug('Sending batch ' + ((i / batchSize) + 1) + ' of ' + batches + ' with ' + batch.size() + ' message(s).');
            System.debug('-- requestBody ---');
            System.debug(req.getBody());

            if (Test.IsRunningTest()) {
                // In tests, skip actual callout but continue loop to validate chunking logic
                continue;
            }

            try {
                HTTPResponse response = http.send(req);
                if (response != null && response.getStatusCode() == 200) {
                    System.debug(functionEndpoint + ' called successfully for batch ' + ((i / batchSize) + 1));
                } else {
                    System.debug(
                        LoggingLevel.ERROR,
                        'Error occurred for batch ' + ((i / batchSize) + 1) + ': ' +
                        (response != null ? response.getStatus() + ' ' + response.getStatusCode() : 'No response')
                    );
                    if (response != null) {
                        System.debug('Response body: ' + response.getBody());
                    }
                }
            } catch (Exception ex) {
                System.debug(LoggingLevel.ERROR, 'Exception during callout for batch ' + ((i / batchSize) + 1) + ': ' + ex.getMessage());
            }
        }
    }

    private GoogleAuthResponse getGCPAuthToken(USPF_PubSub_Setting__mdt serviceAccount) {
        GoogleAuthResponse result = new GoogleAuthResponse();

        Organization org = [SELECT IsSandbox FROM Organization LIMIT 1];

        Auth.JWT jwt = new Auth.JWT();
        jwt.setAud(serviceAccount.Token_Endpoint__c);
        jwt.setIss(serviceAccount.Client_Email__c);

        // Additional claims to set scope
        Map<String, Object> claims = new Map<String, Object>();
        claims.put('scope', serviceAccount.Scope__c);
        jwt.setAdditionalClaims(claims);

        // Create the object that signs the JWT bearer token
        Auth.JWS jws = new Auth.JWS(jwt, 'uspf_pubsub');

        // Get the resulting JWS in case debugging is required
        String token = jws.getCompactSerialization();

        // Set the token endpoint that the JWT bearer token is posted to
        String tokenEndpoint = serviceAccount.Token_Endpoint__c;

        Auth.JWTBearerTokenExchange bearer = new Auth.JWTBearerTokenExchange(tokenEndpoint, jws);

        if (Test.IsRunningTest()) {
            result.access_token = '123456';
        } else {
            System.HttpResponse response = bearer.getHTTPResponse();

            result = (GoogleAuthResponse) JSON.deserialize(response.getBody(), GoogleAuthResponse.class);
        	System.debug('token');
            System.debug(result.access_token);
        }
        return result;
    }

    // public void getAccessToken() {
    public void execute(QueueableContext context) {
        USPF_PubSub_Setting__mdt serviceAccount = this.getUSPFServiceAccount();

        String functionEndpoint = serviceAccount.Function_Endpoint__c;

        GoogleAuthResponse authResponse = this.getGCPAuthToken(serviceAccount);

        if (authResponse.access_token != null) {
            this.sendMessage(
                authResponse.access_token,
                functionEndpoint
            );
        }
    }

    public class USPFPubSubException extends Exception {
    }
}