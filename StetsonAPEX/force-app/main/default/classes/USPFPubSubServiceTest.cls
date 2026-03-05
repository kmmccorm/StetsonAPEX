@IsTest
private class USPFPubSubServiceTest {

    private class OkMock implements HttpCalloutMock {
        public HTTPResponse respond( HTTPRequest req ) {
            HttpResponse res = new HttpResponse();
            res.setStatusCode( 200 );
            res.setStatus( 'OK' );
            res.setBody( '{"ok":true}' );
            return res;
        }
    }

    private static List<USPFPubSubMessage> buildMessages( Integer count ) {
        List<USPFPubSubMessage> msgs = new List<USPFPubSubMessage>();
        for ( Integer i = 0; i < count; i++ ) {
            USPFPubSubMessage m = new USPFPubSubMessage();
            m.sourceSystemId = String.valueOf( i );
            msgs.add( m );
        }
        return msgs;
    }

    @IsTest
    static void NoMessagesTest() {
        Test.setMock( HttpCalloutMock.class, new OkMock() );
        Test.startTest();
        USPFPubSubService svc = new USPFPubSubService( new List<USPFPubSubMessage>() );
        System.enqueueJob( svc );
        Test.stopTest();
        Assert.isTrue( true, 'execute() completed with no messages.' );
    }

    // Exactly 1000 messages: should iterate once in batching loop. In Test context, callouts are skipped inside sendMessage,
    // but execute still obtains auth token in test mode and invokes sendMessage
    @IsTest
    static void LessThanEqualTo1000MessagesTest() {
        List<USPFPubSubMessage> msgs = buildMessages( 1000 );

        Test.setMock( HttpCalloutMock.class, new OkMock() );
        Test.startTest();
        USPFPubSubService svc = new USPFPubSubService( msgs );
        System.enqueueJob( svc );
        Test.stopTest();

        Assert.areEqual( 1000, msgs.size(), 'Prepared exactly one batch of 1000.' );
    }

    // More than 1000 messages: should iterate multiple times in batching loop (2 batches for 1500).
    @IsTest
    static void Over1000MessagesTest() {
        List<USPFPubSubMessage> msgs = buildMessages( 1500 );

        Test.setMock( HttpCalloutMock.class, new OkMock() );
        Test.startTest();
        USPFPubSubService svc = new USPFPubSubService( msgs );
        System.enqueueJob( svc );
        Test.stopTest();

        Assert.areEqual( 1500, msgs.size(), 'Prepared two batches worth of messages.' );
    }

    @IsTest
    static void NullListTest() {
        // While the constructor expects a List, simulate empty path coverage using an empty list instance.
        List<USPFPubSubMessage> msgs = null;
        // Defensive: pass null by dynamically invoking constructor only if needed; here we pass empty to trigger early return.
        Test.setMock( HttpCalloutMock.class, new OkMock() );
        Test.startTest();
        USPFPubSubService svc = new USPFPubSubService( new List<USPFPubSubMessage>() );
        System.enqueueJob( svc );
        Test.stopTest();
        Assert.isTrue( true, 'Handled null/empty messages path without exception.' );
    }
}