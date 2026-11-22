IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121212103_InitialCreate'
)
BEGIN
    CREATE TABLE [Admins] (
        [Id] int NOT NULL IDENTITY,
        [Email] nvarchar(160) NOT NULL,
        [FullName] nvarchar(120) NOT NULL,
        [PasswordHash] nvarchar(max) NOT NULL,
        [SecurityStamp] nvarchar(40) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [LastLoginAt] datetime2 NULL,
        CONSTRAINT [PK_Admins] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121212103_InitialCreate'
)
BEGIN
    CREATE TABLE [Categories] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(120) COLLATE Turkish_CI_AS NOT NULL,
        [Slug] nvarchar(140) NOT NULL,
        [Description] nvarchar(1000) NULL,
        [ImageUrl] nvarchar(300) NULL,
        [IsActive] bit NOT NULL,
        [DisplayOrder] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Categories] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121212103_InitialCreate'
)
BEGIN
    CREATE TABLE [ContactMessages] (
        [Id] int NOT NULL IDENTITY,
        [FullName] nvarchar(120) NOT NULL,
        [Email] nvarchar(160) NOT NULL,
        [Phone] nvarchar(30) NULL,
        [Subject] nvarchar(60) NOT NULL,
        [Message] nvarchar(3000) NOT NULL,
        [IsRead] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_ContactMessages] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121212103_InitialCreate'
)
BEGIN
    CREATE TABLE [ContentPages] (
        [Id] int NOT NULL IDENTITY,
        [Title] nvarchar(160) COLLATE Turkish_CI_AS NOT NULL,
        [Slug] nvarchar(160) NOT NULL,
        [Content] nvarchar(max) NOT NULL,
        [ShowInFooter] bit NOT NULL,
        [FooterOrder] int NOT NULL,
        [IsActive] bit NOT NULL,
        [IsSystem] bit NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_ContentPages] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121212103_InitialCreate'
)
BEGIN
    CREATE TABLE [EmailLogs] (
        [Id] int NOT NULL IDENTITY,
        [ToEmail] nvarchar(160) NOT NULL,
        [Subject] nvarchar(200) NOT NULL,
        [Body] nvarchar(max) NOT NULL,
        [Category] nvarchar(40) NOT NULL,
        [Success] bit NOT NULL,
        [Error] nvarchar(500) NULL,
        [SentBy] nvarchar(160) NULL,
        [UserId] int NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_EmailLogs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121212103_InitialCreate'
)
BEGIN
    CREATE TABLE [Notes] (
        [Id] int NOT NULL IDENTITY,
        [Title] nvarchar(160) NULL,
        [Content] nvarchar(4000) NOT NULL,
        [IsPinned] bit NOT NULL,
        [IsDone] bit NOT NULL,
        [Color] nvarchar(20) NOT NULL,
        [CreatedBy] nvarchar(160) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Notes] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121212103_InitialCreate'
)
BEGIN
    CREATE TABLE [SiteSettings] (
        [Key] nvarchar(60) NOT NULL,
        [Value] nvarchar(2000) NULL,
        CONSTRAINT [PK_SiteSettings] PRIMARY KEY ([Key])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121212103_InitialCreate'
)
BEGIN
    CREATE TABLE [Users] (
        [Id] int NOT NULL IDENTITY,
        [FirstName] nvarchar(60) COLLATE Turkish_CI_AS NOT NULL,
        [LastName] nvarchar(60) COLLATE Turkish_CI_AS NOT NULL,
        [Email] nvarchar(160) NOT NULL,
        [Phone] nvarchar(30) NULL,
        [CompanyName] nvarchar(160) COLLATE Turkish_CI_AS NULL,
        [TaxNumber] nvarchar(30) NULL,
        [TaxOffice] nvarchar(80) NULL,
        [City] nvarchar(60) COLLATE Turkish_CI_AS NULL,
        [PasswordHash] nvarchar(max) NOT NULL,
        [SecurityStamp] nvarchar(40) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [IsActive] bit NOT NULL,
        [IsApproved] bit NOT NULL,
        [ApprovedAt] datetime2 NULL,
        [ApprovedBy] nvarchar(160) NULL,
        [AdminNotes] nvarchar(4000) NULL,
        [LastLoginAt] datetime2 NULL,
        [EmailConfirmed] bit NOT NULL,
        [EmailConfirmedAt] datetime2 NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121212103_InitialCreate'
)
BEGIN
    CREATE TABLE [Products] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(160) COLLATE Turkish_CI_AS NOT NULL,
        [Slug] nvarchar(180) NOT NULL,
        [Sku] nvarchar(40) COLLATE Turkish_CI_AS NOT NULL,
        [Subtitle] nvarchar(80) COLLATE Turkish_CI_AS NULL,
        [ShortDescription] nvarchar(500) NULL,
        [Description] nvarchar(max) NULL,
        [Features] nvarchar(4000) NULL,
        [Details] nvarchar(4000) NULL,
        [Price] decimal(18,2) NOT NULL,
        [StockQuantity] int NOT NULL,
        [Barcode] nvarchar(64) NULL,
        [CriticalStockLevel] int NOT NULL,
        [MinOrderQuantity] int NOT NULL,
        [PackSize] int NULL,
        [Badge] nvarchar(30) NULL,
        [IsActive] bit NOT NULL,
        [IsFeatured] bit NOT NULL,
        [CategoryId] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [StockUpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Products] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Product_Critical] CHECK ([CriticalStockLevel] >= 0),
        CONSTRAINT [CK_Product_MinOrder] CHECK ([MinOrderQuantity] >= 1),
        CONSTRAINT [CK_Product_Price] CHECK ([Price] >= 0),
        CONSTRAINT [CK_Product_Stock] CHECK ([StockQuantity] >= 0),
        CONSTRAINT [FK_Products_Categories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [Categories] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121212103_InitialCreate'
)
BEGIN
    CREATE TABLE [Addresses] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [Title] nvarchar(60) NOT NULL,
        [FullName] nvarchar(120) NOT NULL,
        [Phone] nvarchar(30) NOT NULL,
        [City] nvarchar(60) NOT NULL,
        [District] nvarchar(60) NOT NULL,
        [PostalCode] nvarchar(10) NULL,
        [AddressLine] nvarchar(400) NOT NULL,
        [IsDefault] bit NOT NULL,
        CONSTRAINT [PK_Addresses] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Addresses_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121212103_InitialCreate'
)
BEGIN
    CREATE TABLE [Carts] (
        [Id] int NOT NULL IDENTITY,
        [Token] nvarchar(40) NOT NULL,
        [UserId] int NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Carts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Carts_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121212103_InitialCreate'
)
BEGIN
    CREATE TABLE [EmailVerificationCodes] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [CodeHash] nvarchar(100) NOT NULL,
        [ExpiresAt] datetime2 NOT NULL,
        [Attempts] int NOT NULL,
        [UsedAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_EmailVerificationCodes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_EmailVerificationCodes_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121212103_InitialCreate'
)
BEGIN
    CREATE TABLE [Orders] (
        [Id] int NOT NULL IDENTITY,
        [OrderNumber] nvarchar(20) NOT NULL,
        [UserId] int NULL,
        [Status] int NOT NULL,
        [PaymentStatus] int NOT NULL,
        [PaymentMethod] nvarchar(60) NOT NULL,
        [FullName] nvarchar(120) COLLATE Turkish_CI_AS NOT NULL,
        [Email] nvarchar(160) NOT NULL,
        [Phone] nvarchar(30) NOT NULL,
        [CompanyName] nvarchar(160) COLLATE Turkish_CI_AS NULL,
        [TaxNumber] nvarchar(30) NULL,
        [AddressLine] nvarchar(400) NOT NULL,
        [City] nvarchar(60) NOT NULL,
        [District] nvarchar(60) NOT NULL,
        [PostalCode] nvarchar(10) NULL,
        [Notes] nvarchar(1000) NULL,
        [AdminNote] nvarchar(1000) NULL,
        [Subtotal] decimal(18,2) NOT NULL,
        [ShippingCost] decimal(18,2) NOT NULL,
        [Total] decimal(18,2) NOT NULL,
        [IdempotencyKey] nvarchar(40) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Orders] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Orders_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121212103_InitialCreate'
)
BEGIN
    CREATE TABLE [PasswordResetTokens] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [TokenHash] nvarchar(100) NOT NULL,
        [ExpiresAt] datetime2 NOT NULL,
        [UsedAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_PasswordResetTokens] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PasswordResetTokens_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121212103_InitialCreate'
)
BEGIN
    CREATE TABLE [ProductColors] (
        [Id] int NOT NULL IDENTITY,
        [ProductId] int NOT NULL,
        [Name] nvarchar(40) NOT NULL,
        [HexCode] nvarchar(9) NOT NULL,
        [SortOrder] int NOT NULL,
        [Barcode] nvarchar(64) NULL,
        [StockQuantity] int NOT NULL,
        CONSTRAINT [PK_ProductColors] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_ProductColor_Stock] CHECK ([StockQuantity] >= 0),
        CONSTRAINT [FK_ProductColors_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121212103_InitialCreate'
)
BEGIN
    CREATE TABLE [ProductImages] (
        [Id] int NOT NULL IDENTITY,
        [ProductId] int NOT NULL,
        [Url] nvarchar(300) NOT NULL,
        [AltText] nvarchar(200) NULL,
        [SortOrder] int NOT NULL,
        CONSTRAINT [PK_ProductImages] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProductImages_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121212103_InitialCreate'
)
BEGIN
    CREATE TABLE [StockMovements] (
        [Id] int NOT NULL IDENTITY,
        [ProductId] int NOT NULL,
        [VariantId] int NULL,
        [VariantName] nvarchar(40) NULL,
        [Type] int NOT NULL,
        [Quantity] int NOT NULL,
        [StockBefore] int NOT NULL,
        [StockAfter] int NOT NULL,
        [Note] nvarchar(300) NULL,
        [CreatedBy] nvarchar(160) NULL,
        [OrderId] int NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_StockMovements] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_StockMovements_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121212103_InitialCreate'
)
BEGIN
    CREATE TABLE [CartItems] (
        [Id] int NOT NULL IDENTITY,
        [CartId] int NOT NULL,
        [ProductId] int NOT NULL,
        [ColorName] nvarchar(40) NOT NULL,
        [Quantity] int NOT NULL,
        [AddedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_CartItems] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_CartItem_Qty] CHECK ([Quantity] > 0),
        CONSTRAINT [FK_CartItems_Carts_CartId] FOREIGN KEY ([CartId]) REFERENCES [Carts] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_CartItems_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121212103_InitialCreate'
)
BEGIN
    CREATE TABLE [OrderItems] (
        [Id] int NOT NULL IDENTITY,
        [OrderId] int NOT NULL,
        [ProductId] int NULL,
        [ProductName] nvarchar(160) NOT NULL,
        [Sku] nvarchar(40) NOT NULL,
        [ColorName] nvarchar(40) NOT NULL,
        [ImageUrl] nvarchar(300) NULL,
        [UnitPrice] decimal(18,2) NOT NULL,
        [Quantity] int NOT NULL,
        [LineTotal] decimal(18,2) NOT NULL,
        CONSTRAINT [PK_OrderItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OrderItems_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_OrderItems_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121212103_InitialCreate'
)
BEGIN
    CREATE TABLE [OrderStatusHistories] (
        [Id] int NOT NULL IDENTITY,
        [OrderId] int NOT NULL,
        [Status] int NOT NULL,
        [ChangedBy] nvarchar(160) NULL,
        [Note] nvarchar(500) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_OrderStatusHistories] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OrderStatusHistories_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121212103_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Addresses_UserId] ON [Addresses] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121212103_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Admins_Email] ON [Admins] ([Email]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121212103_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CartItems_CartId_ProductId_ColorName] ON [CartItems] ([CartId], [ProductId], [ColorName]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121212103_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CartItems_ProductId] ON [CartItems] ([ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121212103_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Carts_Token] ON [Carts] ([Token]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121212103_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Carts_UserId] ON [Carts] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121212103_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Categories_IsActive_DisplayOrder] ON [Categories] ([IsActive], [DisplayOrder]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121212103_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Categories_Slug] ON [Categories] ([Slug]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121212103_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ContentPages_IsActive_ShowInFooter_FooterOrder] ON [ContentPages] ([IsActive], [ShowInFooter], [FooterOrder]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121212103_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ContentPages_Slug] ON [ContentPages] ([Slug]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121212103_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_EmailLogs_CreatedAt] ON [EmailLogs] ([CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121212103_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_EmailLogs_UserId] ON [EmailLogs] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121212103_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_EmailVerificationCodes_UserId_CreatedAt] ON [EmailVerificationCodes] ([UserId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121212103_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_OrderItems_OrderId] ON [OrderItems] ([OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121212103_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_OrderItems_ProductId] ON [OrderItems] ([ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121212103_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Orders_IdempotencyKey] ON [Orders] ([IdempotencyKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121212103_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Orders_OrderNumber] ON [Orders] ([OrderNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121212103_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Orders_Status] ON [Orders] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121212103_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Orders_UserId_CreatedAt] ON [Orders] ([UserId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121212103_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_OrderStatusHistories_OrderId] ON [OrderStatusHistories] ([OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121212103_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PasswordResetTokens_TokenHash] ON [PasswordResetTokens] ([TokenHash]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121212103_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_PasswordResetTokens_UserId] ON [PasswordResetTokens] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121212103_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_ProductColors_Barcode] ON [ProductColors] ([Barcode]) WHERE [Barcode] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121212103_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ProductColors_ProductId_Name] ON [ProductColors] ([ProductId], [Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121212103_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ProductImages_ProductId] ON [ProductImages] ([ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121212103_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Products_Barcode] ON [Products] ([Barcode]) WHERE [Barcode] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121212103_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Products_CategoryId] ON [Products] ([CategoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121212103_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Products_IsActive_CategoryId] ON [Products] ([IsActive], [CategoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121212103_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Products_Sku] ON [Products] ([Sku]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121212103_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Products_Slug] ON [Products] ([Slug]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121212103_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_StockMovements_CreatedAt_Type] ON [StockMovements] ([CreatedAt], [Type]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121212103_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_StockMovements_ProductId_CreatedAt] ON [StockMovements] ([ProductId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121212103_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_Email] ON [Users] ([Email]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121212103_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Users_IsApproved] ON [Users] ([IsApproved]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121212103_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261121212103_InitialCreate', N'10.0.15');
END;

COMMIT;
GO

