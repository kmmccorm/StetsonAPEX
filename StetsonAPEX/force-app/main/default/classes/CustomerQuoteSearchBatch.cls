public class CustomerQuoteSearchBatch implements Database.Batchable<FIF_Parameters__mdt>,Database.Stateful, Database.AllowsCallouts,Schedulable{
    Map<String,Opportunity> quotes = new Map<String,Opportunity>();
    Map<String,List<Stetson_PFA_Policy__c>> policies = new Map<String,List<Stetson_PFA_Policy__c>>();
    public List<FIF_Parameters__mdt> params = new List<FIF_Parameters__mdt>();

    public void execute(SchedulableContext sc){
        CustomerQuoteSearchBatch bat = new CustomerQuoteSearchBatch();
        Database.executeBatch(bat);
    }

    public CustomerQuoteSearchBatch(){
        system.debug('#### IN CONSTRUCTOR');
        params.add(FIF_Parameters__mdt.getInstance('Master'));
    }

    public List<FIF_Parameters__mdt> start(Database.BatchableContext bc){
        system.debug('#### IN START');
        return params;
    }
    public void execute(Database.BatchableContext info, List<FIF_Parameters__mdt> params){
        system.debug('#### START EXECUTE');
        system.debug(params);
        String startDate = null;
        String endDate = null;
        String startTime = null;
        String endTime = null;
        //Query Previous Run Time
        String className = String.valueOf(this).split(':')[0];
        system.debug('#### ClassName: ' + className);
        List<AsyncApexJob> lastRun = [SELECT Id,ApexClass.Name,CompletedDate FROM AsyncApexJob WHERE ApexClass.Name =: className AND CompletedDate != null ORDER BY CompletedDate DESC LIMIT 1];
        
        if(params[0].Quote_Number__c == null){
            if(!lastRun.isEmpty()){
                DateTime dt = lastRun[0].CompletedDate;
                system.debug('#### LAST RUN TIME: ' + dt);
                startTime = dt.addHours(-1).format('yyyy-MM-dd hh:mm:ss a');
                endTime = dt.addHours(2).format('yyyy-MM-dd hh:mm:ss a');
                
                system.debug('#### Start: ' + startTime);
                system.debug('#### End: ' + endTime);
            }
            
            startDate = params[0].Start_Date_Time__c != null ? String.valueOf(params[0].Start_Date_Time__c) : startTime;
            endDate = params[0].End_Date_Time__c != null ? String.valueOf(params[0].End_Date_Time__c) : endTime;
        }
        
        
        String quoteNumber = params[0].Quote_Number__c != null ? params[0].Quote_Number__c : null;
        Integer maxRow = params[0].Max_Row__c != null ? Integer.valueOf(params[0].Max_Row__c) : null;

        string customerName = null; 
        string customerId = null; 
        string quoteType = 'Original';
        FirstInsuranceFunding.PBSWebService_FIFCSoap so = new FirstInsuranceFunding.PBSWebService_FIFCSoap();
        system.debug('#### startDate: ' + startDate);
        system.debug('#### endDate: ' + endDate);
        FirstInsuranceFunding.Response s = so.CustomerQuoteSearch(getAuth(),maxRow,quoteNumber,customerName,customerId,quoteType,startDate,endDate);
        Dom.Document doc = new Dom.Document();
        // system.debug('#### DATA');
        // system.debug(JSON.serializePretty(s.Data));
        doc.load(s.Data);
        DOM.XMLNode rootElement = doc.getRootElement();
        //Map<String,Opportunity> quotes = new Map<String,Opportunity>();
        
        for ( Dom.XMLNode childElement : rootElement.getChildElements() ) {
            //root elements of each array
            //system.debug('#### Parent: ' + childElement.getName() + '-' + childElement.getText());
            for (Dom.XMLNode detailElement : childElement.getChildElements()){
                //childs elements of each array
                String quoteID = '';
                //system.debug('#### Child: ' + detailElement.getName() + '-' + detailElement.getText());
                if(detailElement.getName() == 'CustomerQuote'){
                    //system.debug('#### Child: ' + detailElement.getName() + '-' + detailElement.getText());
                    Opportunity opp = new Opportunity();
                    String BrokerContactFirstName= '';
                    String BrokerContactLastName='';
                    String oppName = '';
                    for (Dom.XMLNode cq : detailElement.getChildElements()){
                        //childs elements of each childs array
                        //system.debug('####: ' + cq.getName() + ' : ' + cq.getText());
                        if(cq.getName() == 'QuoteNumber'){
                            opp.Quote_Number__c = cq.getText().trim();
                            opp.Quote_Id__c = cq.getText().trim();
                            oppName += cq.getText().trim();
                        }
                        if(cq.getName() == 'CustomerName'){
                            opp.First_Named_Insured__c = cq.getText().trim();
                            oppName += ' - ' + cq.getText().trim();
                        }
                        if(cq.getName() == 'QuoteCreatedDate'){
                            opp.Quote_Created_Date__c = Date.parse(cq.getText().trim());
                        }
                        if(cq.getName() == 'QuoteLastModifiedDate'){
                            opp.Quote_Last_Modified_Date__c = Date.parse(cq.getText().trim());
                        }
                        if(cq.getName() == 'EffectiveDate'){
                            opp.Effective_Date__c = Date.parse(cq.getText().trim());
                            opp.CloseDate = Date.parse(cq.getText().trim());
                        }
                        if(cq.getName() == 'TotalPremium'){
                            opp.Total_Premium__c = Decimal.valueOf(cq.getText().trim().replace(',',''));
                        }
                        if(cq.getName() == 'DownPaymentAmount'){
                            opp.Down_Payment__c = Decimal.valueOf(cq.getText().trim().replace(',',''));
                        }
                        if(cq.getName() == 'NumberOfInstallments'){
                            opp.Number_of_Installments__c = Integer.valueOf(cq.getText().trim());
                        }
                        if(cq.getName() == 'BrokerContactFirstName'){
                            BrokerContactFirstName = cq.getText().trim();
                        }
                        if(cq.getName() == 'BrokerContactLastName'){
                            BrokerContactLastName = cq.getText().trim();
                        }
                        if(cq.getName() == 'QuoteForName'){
                            opp.Quote_For_Name__c = cq.getText().trim();
                        }
                        if(cq.getName() == 'AgentName'){
                            opp.Retail_Agent_Contact_Name__c = cq.getText().trim();
                        }
                        if(cq.getName() == 'IsOriginal'){
                            opp.FIF_Original_Quote__c = Boolean.valueOf(cq.getText().trim());
                        }
                        if(cq.getName() == 'QuoteStatus'){
                            opp.PFA_Status__c = cq.getText().trim();
                        }
                        if(cq.getName() == 'DocuSignStatus'){
                            opp.DocuSign_Status__c = cq.getText().trim();
                        }
                        if(cq.getName() == 'PaymentStatus'){
                            opp.Payment_Status__c = cq.getText().trim();
                        }
                        if(cq.getName() == 'AvailablePaymentOptions'){
                            opp.Available_Payment_Options__c = cq.getText().trim();
                        }
                        if(cq.getName() == 'PayInFullReason'){
                            opp.Pay_In_Full_Reason__c = cq.getText().trim();
                        }
                        if(cq.getName() == 'LoanNumber'){
                            opp.Loan_Number__c = cq.getText().trim();
                        }
                    }
                    if(Test.isRunningTest()){
                        opp.StageName = 'Quote Released to RT/Agent';
                    }
                    
                    opp.Name = oppName;
                    //opp.Quote_Status__c = 'New Quote';
                    //opp.Quote_Approver__c = 'RYAN Integration';
                    opp.RecordTypeId = Schema.SObjectType.Opportunity.getRecordTypeInfosByName().get('Stetson').getRecordTypeId();
                    opp.Ryan_Employee_Name__c = BrokerContactFirstName + ' ' + BrokerContactLastName;
                    quotes.put(opp.Quote_Number__c,opp);
                }    
            } 
        }
    }

    public void finish(Database.BatchableContext info) {
        FIFIntegationBatch b = new FIFIntegationBatch(quotes);
        Database.executeBatch(b,5);
    }

    public static FirstInsuranceFunding.Authentication getAuth(){
        FIF_Auth__mdt auth = FIF_Auth__mdt.getInstance('Admin');
        FirstInsuranceFunding.Authentication fifAuth = new FirstInsuranceFunding.Authentication();
        fifAuth.UserName = auth.Username__c;
        fifAuth.UserPassword = auth.Password__c;
        fifAuth.PortfolioCode = '900';
        fifAuth.UserType = 'Entity';
        return fifAuth;
    }
}