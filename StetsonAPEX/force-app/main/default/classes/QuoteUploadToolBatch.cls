global class QuoteUploadToolBatch implements Database.Batchable<Opportunity>, Database.AllowsCallouts, Database.Stateful {
    private List<Opportunity> quoteList;

    private List<Id> successes = new List<Id>();
    private List<Id> failures = new List<Id>();
    private Map<Id, Map<String, String>> failuresMap = new Map<Id, Map<String, String>>();
    private static String result = '';

    private String infoSource = '';
    private List<USPFPubSubMessage> infoForPubSub = new List<USPFPubSubMessage>();
    
    FinancePro_Settings__mdt auth;

    global QuoteUploadToolBatch(List<Opportunity> quoteList, String source) {
        this.quoteList = quoteList;
        System.debug('Source: ' + source);
        if(source == 'AFCO'){
            auth = FinancePro_Settings__mdt.getInstance(source);
            infoSource = source;
        } else {
            auth = FinancePro_Settings__mdt.getInstance('USPF');
            infoSource = 'USPF';
        }
    }

    global Iterable<Opportunity> start(Database.BatchableContext BC) {
        return quoteList;
    }

    global void execute(Database.BatchableContext info, List<Opportunity> quoteList) {
        List<Opportunity> oppsToUpdate = new List<Opportunity>();
        List<Stetson_PFA_Policy__c> policiesToUpdate = new List<Stetson_PFA_Policy__c>();

        for (Opportunity opp : quoteList) {
            try {
                //Build Get Quote Request
                FinanceproWebservice.QuoteServiceSoap quoteService = new FinanceproWebservice.QuoteServiceSoap();
                FinanceproWebservice.Login loginCreds = new FinanceproWebservice.Login();
                loginCreds.LoginName = auth.Username__c;
                loginCreds.LoginPassword = auth.Password__c;
                loginCreds.FirstName = 'First';
                loginCreds.LastName = 'Last';
                loginCreds.Phone = 'Mobile';
                loginCreds.Email = 'fake@email.test';
                loginCreds.UserID = 0;
                FinanceproWebservice.AuthHeader authHead = new FinanceproWebservice.AuthHeader();
                authHead.Key = auth.Importer_Key__c;
                authHead.Login = loginCreds;
                authHead.SiteID = 0;
                authHead.LogImport = false;
                quoteService.AuthHeader = authHead;
                quoteService.endpoint_x = auth.Endpoint_URL__c + '/webservices/QuoteService.asmx';
                FinanceproWebservice.Results res = quoteService.GetQuoteByID(Integer.valueOf(opp.Quote_ID__c));
                String url = quoteService.GetQuoteAgreementURL(Integer.valueOf(opp.Quote_ID__c));

                System.debug('res: ' + res);

                if (res.Errors.Error != null) {
                    System.debug('There was an error');
                    opp.Integration_Error__c = true;
                    opp.Error_Code__c = res.Errors.Error[0].ErrorCode;
                    opp.Error_Description__c = res.Errors.Error[0].Description;
                    opp.Error_Message__c = res.Errors.Error[0].Message;
                    opp.Error_Severity__c = res.Errors.Error[0].Severity;
                    opp.Error_Policy_ID__c = res.Errors.Error[0].PolicyID;
                } else {
                    System.debug('Success!');
                    opp.Name = res.Quote.QuoteNumber + ' ' + res.Quote.Insured.Name;
                    opp.Quote_Number__c = res.Quote.QuoteNumber;
                    opp.StageName = 'Quote Released to RT/Agent';
                    opp.Quote_Status__c = 'New Quote';
                    opp.API_Partner_ID__c = infoSource;
                    opp.Quote_Approver__c = 'Ryan Integration';
                    // opp.Quote_Type__c = res.Quote.AccountType;  //We can only finance Commercial with USPF Formula Field
                    opp.First_Named_Insured__c = res.Quote.Insured.Name;
                    opp.Insured_in_Receivership_Bankruptcy__c = 'No';
                    opp.Retail_Agent_Contact_Name__c = res.Quote.Agency.ContactName;
                    opp.Effective_Date__c = res.Quote.EffectiveDate.date();
                    opp.CloseDate = res.Quote.EffectiveDate.date();
                    opp.Phone_Number__c = res.Quote.Insured.Phone;
                    opp.Email_Address__c = res.Quote.Insured.Email;

                    opp.Mailing_Address__Street__s = res.Quote.Insured.Address1;
                    opp.Mailing_Address__City__s = res.Quote.Insured.City;
                    opp.Mailing_Address__StateCode__s = res.Quote.Insured.State;
                    opp.Mailing_Address__PostalCode__s = res.Quote.Insured.Zip;
                    opp.Mailing_Address__CountryCode__s = res.Quote.Insured.Country;

                    opp.Physical_Address__Street__s = res.Quote.Insured.Address1;
                    opp.Physical_Address__City__s = res.Quote.Insured.City;
                    opp.Physical_Address__StateCode__s = res.Quote.Insured.State;
                    opp.Physical_Address__PostalCode__s = res.Quote.Insured.Zip;
                    opp.Physical_Address__CountryCode__s = res.Quote.Insured.Country;

                    opp.Total_Payment_Amount__c = res.Quote.NumberOfPayments * res.Quote.PaymentAmount;
                    opp.Number_of_Installments__c = res.Quote.NumberOfPayments;
                    opp.Down_Payment__c = res.Quote.DownPayment;
                    opp.Installment_Amount__c = res.Quote.PaymentAmount;
                    opp.Amount_Financed__c = res.Quote.AmountFinanced;
                    opp.APR__c = res.Quote.InterestRate;
                    opp.Finance_Charge__c = res.Quote.FinanceCharge;
                    opp.Agent_Referral_Fee_Amount__c = res.Quote.AgentCompensation;
                    opp.Doc_Stamp_Tax__c = res.Quote.DocStampFee;
                    opp.Quoting_For_Referral_Fee_Amount__c = res.Quote.AgentCompensation;
                    opp.Agent_Code__c = res.Quote.Agency.SearchCode;
                    opp.Policy_Count__c = 0;
                    opp.First_Installment_Due_Date__c = res.Quote.FirstPaymentDue.date();
                    // opp.Financed_Broker_Fee__c = 'placeholder'; //   This is going to be tricky in USPF, but possible, we plan to create a separate policy for this

                    if (res.Quote.Policies.Policy != null && res.Quote.Policies.Policy.size() > 0) {
                        System.debug('Number of Policies: ' + res.Quote.Policies.Policy.size());
                        opp.Policy_Count__c = res.Quote.Policies.Policy.size();
                        opp.Ryan_Company_Name__c = res.Quote.Policies.Policy[0].EnteredByUserName; //"Stetson"  <Quote><Policies><Policy> <EnteredByUserID> FP API Only Returns User ID, maybe possible to build a map?  I'd need to work with USPF DB Team
                        opp.Quote_Create_User__c = res.Quote.Policies.Policy[0].EnteredByUserName; //   <Quote><Policies><Policy> <EnteredByUserID> FP API Only Returns User ID, maybe possible to build a map?

                        Datetime lastModDate = res.Quote.Policies.Policy[0].LastModifiedDate;
                        Decimal totalNonRefundFee = 0;
                        Decimal totalPrem = res.Quote.TotalGrossPremium;
                        Datetime minCreateDate = res.Quote.Policies.Policy[0].DateEntered;
                        List<Stetson_PFA_Policy__c> policies = new List<Stetson_PFA_Policy__c>();

                        Integer currentNum = 1;
                        for (FinanceproWebservice.Policy pol : res.Quote.Policies.Policy) {
                            System.debug('is ' + lastModDate + ' < ' + pol.LastModifiedDate + '?');
                            if (lastModDate < pol.LastModifiedDate) {
                                System.debug('Yes!');
                                lastModDate = pol.LastModifiedDate;
                            }

                            totalNonRefundFee += pol.PolicyFee + pol.BrokerFee + pol.InspectionFee;
                            totalPrem += pol.PolicyFee + pol.BrokerFee + pol.InspectionFee + pol.TaxStampFee;

                            System.debug('is ' + minCreateDate + ' > ' + pol.DateEntered + '?');
                            if (minCreateDate > pol.DateEntered) {
                                System.debug('Yes!');
                                minCreateDate = pol.DateEntered;
                            }

                            Stetson_PFA_Policy__c newPolicy = new Stetson_PFA_Policy__c();
                            newPolicy.Name = pol.PolicyNumber;

                            // if(pol.PolicyNumber.toUpperCase() == 'PENDING'){
                            //     newPolicy.Policy_Number__c = 'USPF ' + res.Quote.QuoteNumber + ' Policy ' + currentNum + ' - PENDING';
                            // } else {
                            newPolicy.Policy_Number__c = pol.PolicyNumber;
                            // }

                            newPolicy.Stetson_PFA__c = opp.Id;
                            newPolicy.Coverage_Code__c = pol.CoverageType; //<Quote><Policies><Policy><CoverageType>
                            newPolicy.Effective_Date__c = pol.InceptionDate?.date(); //<Quote><Policies><Policy><InceptionDate>
                            newPolicy.Policy_Term__c = pol.PolicyTerm; //<Quote><Policies><Policy><PolicyTerm>
                            newPolicy.Carrier_Code__c = pol.InsuranceCompany?.SearchCode; //<Quote><Policies><Policy><InsuranceCompany><SearchCode>
                            newPolicy.GA_Code__c = pol.GeneralAgency?.SearchCode; //<Quote><Policies><Policy><GeneralAgency><SearchCode>
                            newPolicy.Premium__c = pol.GrossPremium; //<Quote><Policies><Policy><GrossPremium>
                            newPolicy.Minimum_Earned_Percentage__c = pol.MinEarnedPercent; //<Quote><Policies><Policy><MinEarnedPercent>
                            newPolicy.Is_Auditable__c = pol.Auditable; //<Quote><Policies><Policy><Auditable>
                            policies.add(newPolicy);

                            currentNum++;

                            System.debug('Policy: ' + newPolicy);
                        }

                        policiesToUpdate.addAll(policies);
                        System.debug('All policies: ' + policies);

                        opp.Quote_Last_Modified_Date__c = lastModDate.date(); //    <Quote><Policies><Policy><LastModifiedDate> Use the Max Modified Date if more than 1 policy
                        opp.Non_Refundable_Fee__c = totalNonRefundFee; //   <PolicyFee> + <BrokerFee> + <InspectionFee> Total from all policies
                        opp.Total_Premium__c = totalPrem; //     <Quote><TotalGrossPremium>+(<Quote><Policies><Policy><PolicyFee> + <Quote><Policies><Policy><BrokerFee> +  <Quote><Policies><Policy><InspectionFee> +<Quote><Policies><Policy> <TaxStampFee>) Total from all policies
                        opp.Quote_Create_Date__c = minCreateDate.date(); // <Quote><Policies><Policy> <DateEntered> Use the Min Entered Date if more than 1 policy
                    }
                    opp.Finance_Agreement_PDF__c = url;

                    opp.Integration_Error__c = false;
                    opp.Error_Code__c = '';
                    opp.Error_Description__c = '';
                    opp.Error_Message__c = '';
                    opp.Error_Severity__c = '';
                    opp.Error_Policy_ID__c = null;

                    USPFPubSubMessage mes = new USPFPubSubMessage();
                    
                    mes.sourceSystemId = opp.Quote_Number__c;
                    mes.documentOwnerId = opp.Quote_Number__c;
                    mes.documentDescription = res.Quote.Insured.Name;
                    mes.filename = opp.Quote_Number__c + '_PFA.pdf';

                    mes.createdAt = DateTime.now().format('yyyy-MM-dd hh:mm:ss.SS').replace(' ', 'T');
                    mes.lastModifiedAt = DateTime.now().format('yyyy-MM-dd hh:mm:ss.SS').replace(' ', 'T');
                    mes.receivedAt = DateTime.now().format('yyyy-MM-dd hh:mm:ss.SS').replace(' ', 'T');
                    mes.sentOn = DateTime.now().format('yyyy-MM-dd hh:mm:ss.SS').replace(' ', 'T');

                    List<USPFPubSubMessage.Versions> versions = new List<USPFPubSubMessage.Versions>();
                    USPFPubSubMessage.Versions ver = new USPFPubSubMessage.Versions();
                    ver.url = url;
                    versions.add(ver);

                    mes.versions = versions;

                    List<USPFPubSubMessage.FileAttributes> allAtts = new List<USPFPubSubMessage.FileAttributes>();
                    USPFPubSubMessage.FileAttributes attLender = new USPFPubSubMessage.FileAttributes();
                    attLender.name = 'Lender';
                    attLender.type = 'string';
                    attLender.value = infoSource;
                    allAtts.add(attLender);

                    USPFPubSubMessage.FileAttributes attAGT = new USPFPubSubMessage.FileAttributes();
                    attAGT.name = 'AGT_Code';
                    attAGT.type = 'string';
                    attAGT.value = String.valueOf(opp.AGT_Number__c);
                    allAtts.add(attAGT);

                    USPFPubSubMessage.FileAttributes attEffective = new USPFPubSubMessage.FileAttributes();
                    attEffective.name = 'Effective';
                    attEffective.type = 'datetime';
                    attEffective.value = String.valueOf(res.Quote.EffectiveDate.date());
                    allAtts.add(attEffective);

                    USPFPubSubMessage.FileAttributes attExpiration = new USPFPubSubMessage.FileAttributes();
                    attExpiration.name = 'Expiration';
                    attExpiration.type = 'datetime';
                    attExpiration.value = String.valueOf(res.Quote.EffectiveDate.date());
                    allAtts.add(attExpiration);

                    USPFPubSubMessage.FileAttributes amtFinanced = new USPFPubSubMessage.FileAttributes();
                    amtFinanced.name = 'Amount_Financed';
                    amtFinanced.type = 'string';
                    amtFinanced.value = String.valueOf(res.Quote.AmountFinanced);
                    allAtts.add(amtFinanced);
                    
                    // new
                    USPFPubSubMessage.FileAttributes attAgent = new USPFPubSubMessage.FileAttributes();
                    attAgent.name = 'AGENT';
                    attAgent.type = 'string';
                    attAgent.value = String.valueOf(res.Quote.Agency.SearchCode);
                    allAtts.add(attAgent);

                    mes.imageRightFileAttributes = allAtts;

                    infoForPubSub.add(mes);
                    System.debug('mes: ' + JSON.serializePretty(mes));
                }
            } catch (Exception e) {
                System.debug('Exception: ' + e);
                opp.Integration_Error__c = true;
                opp.Error_Code__c = e.getTypeName();
                opp.Error_Description__c = e.getStackTraceString();
                opp.Error_Message__c = e.getMessage();
                opp.Error_Severity__c = 'Fatal';
            }

            oppsToUpdate.add(opp);
        }

        if (oppsToUpdate.size() > 0) {
            for (Opportunity opp : oppsToUpdate) {
                System.debug('Opp to update: ' + opp);
            }

            List<Database.SaveResult> saveResults = new List<Database.SaveResult>();
            saveResults = Database.update(oppsToUpdate, false);
            for (Integer i = 0; i < saveResults.size(); i++) {
                if (!saveResults.get(i).isSuccess()) {
                    Map<String, String> errorValues = new Map<String, String>();
                    errorValues.put('Code', String.valueOf(saveResults.get(i).getErrors().get(0).getStatusCode()));
                    errorValues.put('Description', String.valueOf(saveResults.get(i).getErrors().get(0).getFields()));
                    errorValues.put('Message', saveResults.get(i).getErrors().get(0).getMessage());
                    failuresMap.put(oppsToUpdate.get(i).Id, errorValues);
                }
            }
        }

        if (policiesToUpdate.size() > 0) {
            List<Database.UpsertResult> upsertResults = new List<Database.UpsertResult>();
            upsertResults = Database.upsert(policiesToUpdate, Stetson_PFA_Policy__c.Fields.Policy_Number__c, false);

            for (Integer i = 0; i < upsertResults.size(); i++) {
                if (!upsertResults.get(i).isSuccess()) {
                    System.debug('ERROR: ' + upsertResults.get(i).getErrors().get(0).getMessage());
                }
            }
        }
    }

    global void finish(Database.BatchableContext info) {
        System.debug('Failures: ' + failuresMap);

        List<Opportunity> oppsToUpdate = new List<Opportunity>();

        for (String opp : failuresMap.keySet()) {
            Opportunity tempOpp = new Opportunity();
            tempOpp.Id = opp;
            tempOpp.Integration_Error__c = true;
            tempOpp.Error_Code__c = failuresMap.get(opp).get('Code');
            tempOpp.Error_Description__c = failuresMap.get(opp).get('Description');
            tempOpp.Error_Message__c = failuresMap.get(opp).get('Message');
            tempOpp.Error_Severity__c = 'Fatal';
            oppsToUpdate.add(tempOpp);
        }

        if (oppsToUpdate.size() > 0) {
            update oppsToUpdate;
        }

        USPFPubSubService pubSub = new USPFPubSubService(infoForPubSub);
        ID jobID = System.enqueueJob(pubSub);
    }
}