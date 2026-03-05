public class InitiateFIFBatch implements Metadata.DeployCallback{

    @InvocableMethod (label='Run FIF Batch')
    public static void callBatch(List<FlowInput> inputs){
        system.debug(JSON.serializePretty(inputs));

        Metadata.CustomMetadata customMeta = new Metadata.CustomMetadata();
        customMeta.fullName = 'FIF_Parameters__mdt.Master';
        customMeta.label = 'Master';

        Map<String, Object> metadataFieldValueMap = new Map<String, Object>();
        metadataFieldValueMap.put('Start_Date_Time__c',inputs[0].startDate);
        metadataFieldValueMap.put('End_Date_Time__c',inputs[0].endDate);
        metadataFieldValueMap.put('Max_Row__c',inputs[0].maxRow);
        metadataFieldValueMap.put('Quote_Number__c',inputs[0].quoteNumber);

        for(String key : metadataFieldValueMap.keySet()){
            Metadata.CustomMetadataValue cMetadataValue = new Metadata.CustomMetadataValue();
            cMetadataValue.Field = key;
            cMetadataValue.Value = metadataFieldValueMap.get(key); 
            customMeta.values.add(cMetadataValue);
        }
        Metadata.DeployContainer mdContainer = new Metadata.DeployContainer();
        mdContainer.addMetadata(customMeta);
        InitiateFIFBatch callback = new InitiateFIFBatch();
        system.debug('#### META');
        system.debug(JSON.serializePretty(mdContainer));
        Id jobId = Test.isRunningTest() ? null : Metadata.Operations.enqueueDeployment(mdContainer, callback);
        system.debug(jobId);
    }

    //Inteface method 
    public void handleResult(Metadata.DeployResult result, Metadata.DeployCallbackContext context) {
        
        if (result.status == Metadata.DeployStatus.Succeeded) {
            //Success
            System.debug('Success Result-' + result);
            CustomerQuoteSearchBatch bat = new CustomerQuoteSearchBatch();
            Database.executeBatch(bat);
        } else {
            //Failed
            System.debug('Failed Result-' + result);
        }
    }

    public class FlowInput{
        @InvocableVariable
        public DateTime startDate;
        @InvocableVariable
        public DateTime endDate;
        @InvocableVariable
        public Decimal maxRow;
        @InvocableVariable
        public String quoteNumber;
    }
}