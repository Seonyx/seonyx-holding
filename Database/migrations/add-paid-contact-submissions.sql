-- Add PaidContactSubmissions table for Stripe-gated contact form
-- Run manually via SSMS on all environments

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'PaidContactSubmissions')
BEGIN
    CREATE TABLE PaidContactSubmissions (
        Id                      INT IDENTITY(1,1) PRIMARY KEY,
        ReferenceId             NVARCHAR(36)  NOT NULL,
        Name                    NVARCHAR(200) NOT NULL,
        Email                   NVARCHAR(320) NOT NULL,
        Company                 NVARCHAR(200) NULL,
        Subject                 NVARCHAR(500) NOT NULL,
        Message                 NVARCHAR(MAX) NOT NULL,
        IpAddress               NVARCHAR(45)  NULL,
        UserAgent               NVARCHAR(500) NULL,
        StripeCheckoutSessionId NVARCHAR(200) NULL,
        StripePaymentIntentId   NVARCHAR(200) NULL,
        AmountPaid              INT           NULL,
        Status                  NVARCHAR(20)  NOT NULL CONSTRAINT DF_PaidContactSubmissions_Status DEFAULT 'Pending',
        SubmittedDate           DATETIME      NOT NULL CONSTRAINT DF_PaidContactSubmissions_SubmittedDate DEFAULT GETUTCDATE(),
        ProcessedDate           DATETIME      NULL,

        CONSTRAINT UQ_PaidContactSubmissions_ReferenceId UNIQUE (ReferenceId)
    );

    CREATE INDEX IX_PaidContactSubmissions_ReferenceId ON PaidContactSubmissions(ReferenceId);
    CREATE INDEX IX_PaidContactSubmissions_Status      ON PaidContactSubmissions(Status);

    PRINT 'PaidContactSubmissions table created.';
END
ELSE
BEGIN
    PRINT 'PaidContactSubmissions table already exists -- skipped.';
END
