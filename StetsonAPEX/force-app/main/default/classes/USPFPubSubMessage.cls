public class USPFPubSubMessage {
    public String sourceSystem = 'Stetson';
    public String sourceSystemId;
    public String sourceSystemEnvironment = 'PROD';
    public String documentOwner = 'Stetson';
    public String documentOwnerId;
    public String documentCategory = 'PFA';
    public String documentType = 'PFA';
    public String documentDescription;
    public String description = 'PFA';
    public String fileName;
    public String createdAt;
    public String lastModifiedAt;
    public List<Versions> versions;
    public boolean isDeleted = false;
    // public String createdBy = 'USPF';
    public String receivedAt;
    public String sentOn;
    public String sourceSystemIdType;
    public List<FileAttributes> imageRightFileAttributes;

    public class Versions {
        public Integer version = 0;
        public String url;
        public String hash = 'w9HgCM5HAWhIQpBUh8Zvbg==';
        public Integer size = 16812;
        public String uuid = '5bdd543d-cd14-4af3-a778-02c91b098bda';
    }

    public class FileAttributes {
        public String name;
        public String value;
        public String type;
    }
}