public with sharing class FIFIntegationBatch implements Database.Batchable<Opportunity>,Database.Stateful, Database.AllowsCallouts,Metadata.DeployCallback{
    private Map<String,Opportunity> quotes;
    
    public FIFIntegationBatch(Map<String,Opportunity> quotes){
        this.quotes = quotes;
    }

    public Iterable<Opportunity> start(Database.BatchableContext bc){
        //system.debug('#### IN BATCH');
        //system.debug(JSON.serializePretty(quotes));
        return quotes.values();
    }
    public void execute(Database.BatchableContext info, List<Opportunity> quoteList){
        Map<String,Opportunity> quoteMap = new Map<String,Opportunity>();
        Map<String,List<Stetson_PFA_Policy__c>> policies = new Map<String,List<Stetson_PFA_Policy__c>>();
        
        for(Opportunity o: quoteList){
            //system.debug('#### THIS OPP: ' + o.Quote_Number__c);
            
            FirstInsuranceFunding2.PBSWebServiceSoap fif2 = new FirstInsuranceFunding2.PBSWebServiceSoap();
            //Build Get Quote Request
            FirstInsuranceFunding2.QuoteImportRequestOptions opt = new FirstInsuranceFunding2.QuoteImportRequestOptions();
            opt.ReturnQuoteInfo = true;
            opt.ReturnPFA = false;
            FirstInsuranceFunding2.QuoteResponse res = fif2.GetQuote(CustomerQuoteSearchBatch.getAuth(),Integer.valueOf(o.Quote_Number__c),opt);
            //Update Quote Info
            o.Total_Payment_Amount__c = res.QuoteInformation.TotalPaymentAmount;
            o.Amount_Financed__c = res.QuoteInformation.AmountFinanced;
            o.Finance_Charge__c = res.QuoteInformation.FinanceCharge;
            o.Doc_Stamp_Tax__c = res.QuoteInformation.DocStampTax;
            o.Non_Refundable_Fee__c = res.QuoteInformation.NonRefundableFee;
            o.Installment_Amount__c = res.QuoteInformation.InstallmentAmount;
            o.APR__c = res.QuoteInformation.APR;
            o.Agent_Referral_Fee_Amount__c = res.QuoteInformation.AgentReferralFeeAmount;
            o.Quoting_For_Referral_Fee_Amount__c = res.QuoteInformation.QuotingForReferralFeeAmount;
            quoteMap.put(o.Quote_Number__c,o);
            //END Update Quote Info
            String otherInfoString = '<otherInfo>'+res.QuoteInformation.OtherInfo+'</otherInfo>';
            //system.debug(otherInfoString);
            XmlStreamReader reader = new XmlStreamReader(res.QuoteInformation.OtherInfo);
            Dom.Document doc = new Dom.Document();
            doc.load(otherInfoString);
            //parsePolicyXML(doc.getRootElement(),quoteNumber);
            //system.debug('#### End Response');
            DOM.XMLNode rootElement = doc.getRootElement();
            for ( Dom.XMLNode childElement : rootElement.getChildElements() ) {
                //root elements of each array
                //system.debug('#### Policy Parent: ' + childElement.getName() + '-' + childElement.getText());
                if(childElement.getName() == 'ServiceFeePerInstallment'){
                    o.Service_Fee_Per_Installment__c = Decimal.valueOf(childElement.getText().replace(',','').trim());
                }
                if(childElement.getName() == 'New_Renewal'){
                    o.New_or_Renewal__c = childElement.getText().replace(',','').trim();
                }
                if(childElement.getName() == 'IsRenewal'){
                    o.Is_Renewal__c = childElement.getText().trim();
                }
                if(childElement.getName() == 'QuotingForCode'){
                    o.Quoting_For_Code__c = childElement.getText().trim();
                }
                if(childElement.getName() == 'QuotingForAltCode'){
                    o.Quoting_For_Alt_Code__c = childElement.getText().trim().left(8);
                    o.AGT_Number__c = childElement.getText().trim();
                    //LINK TO Account
                    List<Account> acc = new List<Account>([SELECT Id,Name FROM Account WHERE AGT_Code__c =: childElement.getText().trim() LIMIT 1]);
                    if(!acc.isEmpty()){
                        o.AccountId = acc[0].Id;
                        o.Name += ' - ' + acc[0].Name;
                    }
                }
                if(childElement.getName() == 'QuoteCreateUser'){
                    o.Quote_Create_User__c = childElement.getText().trim();
                    o.Ryan_Company_Name__c = childElement.getText().trim();
                }
                if(childElement.getName() == 'QuoteCreateDate'){
                    o.Quote_Create_Date__c = Date.parse(childElement.getText().split(' ')[0].trim());
                }
                if(childElement.getName() == 'AgentCode'){
                    o.Agent_Code__c = childElement.getText().trim();
                }
                if(childElement.getName() == 'AgentContact'){
                    o.Retail_Agent_Contact_Name__c = childElement.getText().trim();
                }
                if(childElement.getName() == 'PolicyCount'){
                    o.Policy_Count__c = Integer.valueOf(childElement.getText().trim());
                }
                if(childElement.getName() == 'EarnedBrokerFee'){
                    o.Earned_Broker_Fee__c = Decimal.valueOf(childElement.getText().replace(',','').trim());
                }
                if(childElement.getName() == 'FinancedBrokerFee'){
                    o.Financed_Broker_Fee__c = Decimal.valueOf(childElement.getText().replace(',','').trim());
                }
                if(childElement.getName() == 'DownPaymentPercentage'){
                    o.Down_Payment_Percentage__c = Decimal.valueOf(childElement.getText().replace(',','').replace('%','').trim());
                }
                // if(childElement.getName() == 'QuoteStatus'){
                //     o.Quote_Status__c = childElement.getText().trim();
                // }
                if(childElement.getName() == 'eCompleteStatus'){
                    o.eComplete_Status__c = childElement.getText().trim();
                }
                if(childElement.getName() == 'FirstInstallmentDueDate'){
                    o.First_Installment_Due_Date__c = Date.parse(childElement.getText().trim());
                }
                if(childElement.getName() == 'RetainedPayments'){
                    o.Retained_Payments__c = Decimal.valueOf(childElement.getText().trim().replace(',',''));
                }

                for (Dom.XMLNode detailElement : childElement.getChildElements()){
                    //system.debug('#### Policy Child: ' + detailElement.getName() + '-' + detailElement.getText());
                    if(detailElement.getName().contains('Address')){
                        if(detailElement.getName().contains('Address1')){
                            //system.debug('#### ADDRESS 1');
                            Boolean isPrimary = false;
                            for(Dom.XMLNode a : detailElement.getChildElements()){
                                //system.debug('####: ' + a.getName() + ' : ' + a.getText());
                                if(a.getName() == 'IsPrimary' && a.getText() == 'True'){
                                    isPrimary = true;
                                }
                                if(isPrimary){
                                    if(a.getName() == 'AddressLine1'){
                                        o.Physical_Address__Street__s = a.getText().trim();
                                    }
                                    if(a.getName() == 'AddressLine2'){
                                        o.Physical_Address__Street__s += '\n' + a.getText().trim();
                                    }
                                    if(a.getName() == 'City'){
                                        o.Physical_Address__City__s = a.getText().trim();
                                    }
                                    if(a.getName() == 'RegionCode'){
                                        o.Physical_Address__StateCode__s = a.getText().trim();
                                    }
                                    if(a.getName() == 'PostalCode'){
                                        o.Physical_Address__PostalCode__s = a.getText().trim();
                                    }
                                    // if(a.getName() == 'Country'){
                                    //     o.Physical_Address__CountryCode__s = a.getText().trim();
                                    // }
                                }
                                else{
                                    if(a.getName() == 'AddressLine1'){
                                        o.Mailing_Address__Street__s = a.getText().trim();
                                    }
                                    if(a.getName() == 'AddressLine2'){
                                        o.Mailing_Address__Street__s += '\n' + a.getText().trim();
                                    }
                                    if(a.getName() == 'City'){
                                        o.Mailing_Address__City__s = a.getText().trim();
                                    }
                                    if(a.getName() == 'RegionCode'){
                                        o.Mailing_Address__StateCode__s = a.getText().trim();
                                    }
                                    if(a.getName() == 'PostalCode'){
                                        o.Mailing_Address__PostalCode__s = a.getText().trim();
                                    }
                                    // if(a.getName() == 'Country'){
                                    //     o.Mailing_Address__CountryCode__s = a.getText().trim();
                                    // }
                                }
                            }
                        }
                        if(detailElement.getName().contains('Address2')){
                            Boolean isPrimary = false;
                            for(Dom.XMLNode a : detailElement.getChildElements()){
                                //system.debug('####: ' + a.getName() + ' : ' + a.getText());
                                if(a.getName() == 'IsPrimary' && a.getText() == 'True'){
                                    isPrimary = true;
                                }
                                else if(a.getName() == 'IsPrimary' && a.getText() == 'False'){
                                    isPrimary = false;
                                }
                                if(isPrimary){
                                    //system.debug('#### PRIMARY TWO');
                                    if(a.getName() == 'AddressLine1'){
                                        system.debug('#### add 3');
                                        o.Physical_Address__Street__s = a.getText().trim();
                                    }
                                    if(a.getName() == 'AddressLine2'){
                                        o.Physical_Address__Street__s += '\n' + a.getText().trim();
                                    }
                                    if(a.getName() == 'City'){
                                        o.Physical_Address__City__s = a.getText().trim();
                                    }
                                    if(a.getName() == 'RegionCode'){
                                        o.Physical_Address__StateCode__s = a.getText().trim();
                                    }
                                    if(a.getName() == 'PostalCode'){
                                        o.Physical_Address__PostalCode__s = a.getText().trim();
                                    }
                                    // if(a.getName() == 'Country'){
                                    //     o.Physical_Address__CountryCode__s = a.getText().trim();
                                    // }
                                }
                                else{
                                    //system.debug('#### Mailing TWO');
                                    if(a.getName() == 'AddressLine1'){
                                        system.debug('#### add 4');
                                        o.Mailing_Address__Street__s = a.getText().trim();
                                    }
                                    if(a.getName() == 'AddressLine2'){
                                        system.debug('#### add 4');
                                        o.Mailing_Address__Street__s += '\n' + a.getText().trim();
                                    }
                                    if(a.getName() == 'City'){
                                        o.Mailing_Address__City__s = a.getText().trim();
                                    }
                                    if(a.getName() == 'RegionCode'){
                                        o.Mailing_Address__StateCode__s = a.getText().trim();
                                    }
                                    if(a.getName() == 'PostalCode'){
                                        o.Mailing_Address__PostalCode__s = a.getText().trim();
                                    }
                                    // if(a.getName() == 'Country'){
                                    //     o.Mailing_Address__CountryCode__s = a.getText().trim();
                                    // }
                                }
                            }
                        }
                    }
                    if(detailElement.getName().contains('Policy')){
                        Stetson_PFA_Policy__c pol = new Stetson_PFA_Policy__c();
                        String policyNumber ='';
                        String thisPolicyCount = detailElement.getName().split('Policy')[1];
                        for(Dom.XMLNode c : detailElement.getChildElements()){
                            //system.debug('#### CHILD??');
                            //system.debug('####: ' + c.getName() + ' : ' + c.getText());
                            if(c.getName() == 'PolicyNumber'){
                                List<String> placeholderPolicies = new List<String>{'tbd','pending','tba','tbd','tbi','tdb','t.b.d.'};
                                if(placeholderPolicies.contains(c.getText().trim().toLowerCase())){
                                    pol.Name = o.Quote_Number__c +' - '+ c.getText().trim()+ ' - ' + thisPolicyCount;
                                }
                                else{
                                    pol.Name = c.getText().trim();
                                }
                                pol.Policy_Number__c = c.getText().trim();
                                policyNumber = c.getText().trim();
                            }
                            if(c.getName() == 'CoverageCode'){
                                pol.Coverage_Code__c = c.getText().trim();
                            }
                            if(c.getName() == 'EffectiveDate'){
                                pol.Effective_Date__c = Date.parse(c.getText().trim());
                            }
                            if(c.getName() == 'PolicyTerm'){
                                pol.Policy_Term__c = Integer.valueOf(c.getText().trim());
                            }
                            if(c.getName() == 'CarrierCode'){
                                pol.Carrier_Code__c = c.getText().trim();
                            }
                            if(c.getName() == 'GACode'){
                                pol.GA_Code__c = c.getText().trim();
                            }
                            if(c.getName() == 'Premium'){
                                pol.Premium__c = Decimal.valueOf(c.getText().replace(',','').trim());
                            }
                            if(c.getName() == 'EarnedTaxesFees'){
                                pol.Earned_Taxes_Fees__c = Decimal.valueOf(c.getText().replace(',','').trim());
                            }
                            if(c.getName() == 'FinancedTaxesFees'){
                                pol.Financed_Taxes_Fees__c = Decimal.valueOf(c.getText().replace(',','').trim());
                            }
                            if(c.getName() == 'MinimumEarnedPremium'){
                                pol.Minimum_Earned_Premium__c = Decimal.valueOf(c.getText().replace(',','').trim());
                            }
                            if(c.getName() == 'MinimumEarnedPercentage'){
                                pol.Minimum_Earned_Percentage__c = Decimal.valueOf(c.getText().replace(',','').replace('%','').trim());
                            }
                            if(c.getName() == 'IsAuditable'){
                                pol.Is_Auditable__c = c.getText().trim() == 'True' ? true : false ;
                            }
                            if(c.getName() == 'IsFilings'){
                                pol.Is_Filings__c = c.getText().trim() == 'True' ? true : false ;
                            }
                            if(c.getName() == 'AdditionalDaysToCancel'){
                                pol.Additional_Days_To_Cancel__c = Integer.valueOf(c.getText().trim());
                            }
                            if(c.getName() == 'AssignedRisk'){
                                pol.Assigned_Risk__c = c.getText().trim() == 'True' ? true : false ;
                            }
                            if(c.getName() == 'Underwriter'){
                                pol.Underwriter__c = c.getText().trim();
                            }
                        }
                        //Add to collection for update
                        if(pol.Policy_Number__c != null){
                            if(policies.containsKey(o.Quote_Number__c)){
                            policies.get(o.Quote_Number__c).add(pol);
                            }
                            else{
                            policies.put(o.Quote_Number__c,new List<Stetson_PFA_Policy__c>{pol});
                            }
                        }
                    } 
                } 
            }
        }
        if(!quoteMap.isEmpty()){
            upsert quoteMap.values() Quote_Number__c;
        }
        if(!policies.isEmpty()){
            List<Stetson_PFA_Policy__c> spp = new List<Stetson_PFA_Policy__c>();
            for(String o: policies.keySet()){
                for(Stetson_PFA_Policy__c p: policies.get(o)){
                    p.Stetson_PFA__c = quoteMap.get(o).Id;
                    spp.add(p);
                }
            }
            //system.debug('#### Policies Size: ' + spp.size());
            system.debug('#### Policies');
            system.debug(JSON.serializePretty(spp)); 
            //updatePolicies(JSON.serialize(spp));
            Database.upsert(spp,Stetson_PFA_Policy__c.Policy_Number__c,false);
        }
        //system.debug(JSON.serializePretty(quoteMap));
        
    }

    public void finish(Database.BatchableContext info) {
        Metadata.CustomMetadata customMeta = new Metadata.CustomMetadata();
        customMeta.fullName = 'FIF_Parameters__mdt.Master';
        customMeta.label = 'Master';

        Map<String, Object> metadataFieldValueMap = new Map<String, Object>();
        metadataFieldValueMap.put('Start_Date_Time__c',null);
        metadataFieldValueMap.put('End_Date_Time__c',null);
        metadataFieldValueMap.put('Max_Row__c',null);
        metadataFieldValueMap.put('Quote_Number__c',null);

        for(String key : metadataFieldValueMap.keySet()){
            Metadata.CustomMetadataValue cMetadataValue = new Metadata.CustomMetadataValue();
            cMetadataValue.Field = key;
            cMetadataValue.Value = metadataFieldValueMap.get(key); 
            customMeta.values.add(cMetadataValue);
        }
        Metadata.DeployContainer mdContainer = new Metadata.DeployContainer();
        mdContainer.addMetadata(customMeta);
        FIFIntegationBatch callback = new FIFIntegationBatch(null);
        system.debug('#### FINISH META');
        system.debug(JSON.serializePretty(mdContainer));
        Id jobId = Test.isRunningTest() ? null : Metadata.Operations.enqueueDeployment(mdContainer, callback);
    }

    //Inteface method 
    public void handleResult(Metadata.DeployResult result, Metadata.DeployCallbackContext context) {

        if (result.status == Metadata.DeployStatus.Succeeded) {
            //Success
            System.debug('FINISH Success Result-' + result);
        } else {
            //Failed
            System.debug('FINISH Failed Result-' + result);
        }
    }
    //iterate through quotes
    //call get quote
    //update quotes and policies

}