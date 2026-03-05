public without sharing class QuoteUploadToolController {
    
    @AuraEnabled
    public static String createOpportunities(Map<String, String> quoteIds, String source){

        System.debug('quoteIds: ' + quoteIds);
        
        List<Opportunity> oppsToUpsert = new List<Opportunity>();
        Id recordTypeId = Schema.SObjectType.Opportunity.getRecordTypeInfosByName().get(System.Label.Quote_Upload_Record_Type_Developer_Name).getRecordTypeId();
        Account genericAcc = [SELECT Id FROM Account WHERE AGT_Code__c = :System.Label.Quote_Upload_Placeholder_AGT_Code];
        Map<String, String> matchingAccsMap = new Map<String, String>();

        for(Account acc: [SELECT Id, AGT_Code__c FROM Account WHERE AGT_Code__c IN :quoteIds.values()]){
            matchingAccsMap.put(acc.AGT_Code__c, acc.Id);
        }

        for(String num: quoteIds.keySet()){
            Opportunity newOpp = new Opportunity();
            newOpp.RecordTypeId = recordTypeId;
            newOpp.Name = 'Placeholder Quote ' + num;
            newOpp.StageName = 'Quote Released to RT/Agent';
            newOpp.AGT_Number__c = quoteIds.get(num);
            newOpp.Quoting_For_Code__c = quoteIds.get(num);
            newOpp.Quoting_For_Alt_Code__c = quoteIds.get(num);
            if(matchingAccsMap.keySet().contains(quoteIds.get(num))){
                newOpp.AccountID = matchingAccsMap.get(quoteIds.get(num));
            } else {
                newOpp.AccountID = genericAcc.Id;
            }
            newOpp.Quote_ID__c = num;
            newOpp.Quote_Number__c = num;
            newOpp.CloseDate = System.today() + 60; //Setting placeholder date to be updated during integration batch
            if(source == 'AFCO'){
                newOpp.API_Partner_ID__c = 'AFCO';
            } else {
                newOpp.API_Partner_ID__c = 'USPF';
            }

            oppsToUpsert.add(newOpp);
        }

        String batchJobId;
        if(oppsToUpsert.size() > 0){
            System.debug(oppsToUpsert);
            upsert oppsToUpsert Quote_ID__c;

            //send opps to batch
            Integer batchSize = Integer.valueOf(System.Label.Quote_Upload_Batch_Size);
            if(batchSize > 50){
                batchSize = 50;
            }
            batchJobId = String.valueOf(Database.executeBatch(new QuoteUploadToolBatch(oppsToUpsert, source), batchSize));
        }
        
        return batchJobId;
    }

    @AuraEnabled(cacheable=true)
    public static String getTemplateDoc(){
        try {
            ContentDocument doc = [SELECT Id, Title, LatestPublishedVersionId FROM ContentDocument WHERE Title = :System.Label.Quote_Upload_Template_File_Name LIMIT 1];

            if(doc != null){
                return '/sfc/servlet.shepherd/version/download/' + doc.LatestPublishedVersionId;
            } else {
                return 'No template found';
            }
        } catch (Exception e) {
            throw new AuraHandledException(e.getMessage());
        }
    }

    @AuraEnabled
    public static List<AsyncApexJob> getJobDetails(String jobId){
        try {
            List<AsyncApexJob> asyncJobList = [SELECT Id, Status, JobItemsProcessed, TotalJobItems,
                                                NumberOfErrors FROM AsyncApexJob where Id =: jobId ];
            return asyncJobList;
        } catch (Exception e) {
            throw new AuraHandledException(e.getMessage());
        }
   }

}