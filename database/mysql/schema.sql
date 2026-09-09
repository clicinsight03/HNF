-- =============================================================================
-- Healing Natural Farms - MySQL schema creation script
-- =============================================================================
-- Target: MySQL 8.0+ (InnoDB, utf8mb4). Matches the EF Core model in
-- HealingNaturalFarms.Infrastructure (AppDbContext + Configurations) and the
-- ASP.NET Core Identity model (AppUser : IdentityUser<int>, AppRole :
-- IdentityRole<int>) as of the MAUI/Email-module milestone.
--
-- This script is a hand-authored equivalent of `dotnet ef database update`,
-- provided because this sandbox has no NuGet/workload access to generate and
-- run an actual EF Core migration. Once you have a normal dev machine with
-- `dotnet-ef` installed, prefer running real migrations
-- (`dotnet ef migrations add InitialCreate` then `dotnet ef database update`)
-- so the migration history table (__EFMigrationsHistory) stays in sync with
-- the model; this script does NOT create that table, so EF will think no
-- migrations have been applied yet if you later mix the two approaches.
--
-- Run with, e.g.:
--   mysql -u root -p < schema.sql
--
-- Enum columns are stored as plain INT by EF Core's default convention
-- (no HasConversion<string> is configured anywhere in the model). The
-- ordinal mapping for each enum is documented next to its column.
-- =============================================================================

CREATE DATABASE IF NOT EXISTS `hnf`
    CHARACTER SET utf8mb4
    COLLATE utf8mb4_unicode_ci;

USE `hnf`;

SET NAMES utf8mb4;

-- =============================================================================
-- ASP.NET Core Identity tables
-- =============================================================================
-- AppDbContext : IdentityDbContext<AppUser, AppRole, int> renames only the
-- user/role tables themselves (ToTable("Users") / ToTable("Roles")); the
-- join/claim/token tables keep Identity's default "AspNet*" names.
--
-- NOTE on LoginProvider/ProviderKey/Name columns below: Identity's own
-- convention is nvarchar(450), but a composite key of two utf8mb4 varchar(450)
-- columns is 3600 bytes - over InnoDB's 3072-byte max index length even with
-- large-prefix/DYNAMIC rows (MySQL 8 default). They're narrowed to
-- VARCHAR(128) here (composite max ~1024 bytes) to keep the keys creatable;
-- this only limits the length of a login-provider name or token name, never
-- of user data.
-- =============================================================================

CREATE TABLE IF NOT EXISTS `Roles` (
    `Id`               INT           NOT NULL AUTO_INCREMENT,
    `Name`             VARCHAR(256)  NULL,
    `NormalizedName`   VARCHAR(256)  NULL,
    `ConcurrencyStamp` TEXT          NULL,
    PRIMARY KEY (`Id`),
    UNIQUE KEY `RoleNameIndex` (`NormalizedName`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='ASP.NET Core Identity roles (AppRole : IdentityRole<int>).';

CREATE TABLE IF NOT EXISTS `Users` (
    `Id`                   INT           NOT NULL AUTO_INCREMENT,
    `FirstName`            VARCHAR(256)  NOT NULL,
    `LastName`             VARCHAR(256)  NOT NULL,
    -- RegionCode: 0 = US, 1 = IN. Pre-selects the visitor's storefront on login.
    `PreferredRegion`      INT           NOT NULL,
    `CreatedAt`            DATETIME(6)   NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `UserName`             VARCHAR(256)  NULL,
    `NormalizedUserName`   VARCHAR(256)  NULL,
    `Email`                VARCHAR(256)  NULL,
    `NormalizedEmail`      VARCHAR(256)  NULL,
    `EmailConfirmed`       TINYINT(1)    NOT NULL DEFAULT 0,
    `PasswordHash`         TEXT          NULL,
    `SecurityStamp`        TEXT          NULL,
    `ConcurrencyStamp`     TEXT          NULL,
    `PhoneNumber`          VARCHAR(32)   NULL,
    `PhoneNumberConfirmed` TINYINT(1)    NOT NULL DEFAULT 0,
    `TwoFactorEnabled`     TINYINT(1)    NOT NULL DEFAULT 0,
    `LockoutEnd`           DATETIME(6)   NULL,
    `LockoutEnabled`       TINYINT(1)    NOT NULL DEFAULT 1,
    `AccessFailedCount`    INT           NOT NULL DEFAULT 0,
    PRIMARY KEY (`Id`),
    UNIQUE KEY `UserNameIndex` (`NormalizedUserName`),
    KEY `EmailIndex` (`NormalizedEmail`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='The real ASP.NET Core Identity user (AppUser : IdentityUser<int>).';

CREATE TABLE IF NOT EXISTS `AspNetRoleClaims` (
    `Id`         INT   NOT NULL AUTO_INCREMENT,
    `RoleId`     INT   NOT NULL,
    `ClaimType`  TEXT  NULL,
    `ClaimValue` TEXT  NULL,
    PRIMARY KEY (`Id`),
    KEY `IX_AspNetRoleClaims_RoleId` (`RoleId`),
    CONSTRAINT `FK_AspNetRoleClaims_Roles_RoleId`
        FOREIGN KEY (`RoleId`) REFERENCES `Roles` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `AspNetUserClaims` (
    `Id`         INT   NOT NULL AUTO_INCREMENT,
    `UserId`     INT   NOT NULL,
    `ClaimType`  TEXT  NULL,
    `ClaimValue` TEXT  NULL,
    PRIMARY KEY (`Id`),
    KEY `IX_AspNetUserClaims_UserId` (`UserId`),
    CONSTRAINT `FK_AspNetUserClaims_Users_UserId`
        FOREIGN KEY (`UserId`) REFERENCES `Users` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `AspNetUserLogins` (
    `LoginProvider`       VARCHAR(128) NOT NULL,
    `ProviderKey`         VARCHAR(128) NOT NULL,
    `ProviderDisplayName` TEXT         NULL,
    `UserId`              INT          NOT NULL,
    PRIMARY KEY (`LoginProvider`, `ProviderKey`),
    KEY `IX_AspNetUserLogins_UserId` (`UserId`),
    CONSTRAINT `FK_AspNetUserLogins_Users_UserId`
        FOREIGN KEY (`UserId`) REFERENCES `Users` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `AspNetUserRoles` (
    `UserId` INT NOT NULL,
    `RoleId` INT NOT NULL,
    PRIMARY KEY (`UserId`, `RoleId`),
    KEY `IX_AspNetUserRoles_RoleId` (`RoleId`),
    CONSTRAINT `FK_AspNetUserRoles_Roles_RoleId`
        FOREIGN KEY (`RoleId`) REFERENCES `Roles` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_AspNetUserRoles_Users_UserId`
        FOREIGN KEY (`UserId`) REFERENCES `Users` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `AspNetUserTokens` (
    `UserId`        INT          NOT NULL,
    `LoginProvider` VARCHAR(128) NOT NULL,
    `Name`          VARCHAR(128) NOT NULL,
    `Value`         TEXT         NULL,
    PRIMARY KEY (`UserId`, `LoginProvider`, `Name`),
    CONSTRAINT `FK_AspNetUserTokens_Users_UserId`
        FOREIGN KEY (`UserId`) REFERENCES `Users` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- =============================================================================
-- Catalog (HealingNaturalFarms.Domain.Entities.Catalog + CatalogConfigurations)
-- =============================================================================

CREATE TABLE IF NOT EXISTS `Categories` (
    `Id`               INT          NOT NULL AUTO_INCREMENT,
    `Name`             VARCHAR(200) NOT NULL,
    `Slug`             VARCHAR(200) NOT NULL,
    -- ProductType: 0 = Plant, 1 = Produce
    `ProductType`      INT          NOT NULL,
    `ParentCategoryId` INT          NULL,
    PRIMARY KEY (`Id`),
    UNIQUE KEY `IX_Categories_Slug` (`Slug`),
    KEY `IX_Categories_ParentCategoryId` (`ParentCategoryId`),
    CONSTRAINT `FK_Categories_Categories_ParentCategoryId`
        FOREIGN KEY (`ParentCategoryId`) REFERENCES `Categories` (`Id`) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='Category tree; ProductType lets Plant/Produce browse separately without walking the tree.';

CREATE TABLE IF NOT EXISTS `Products` (
    `Id`                     INT           NOT NULL AUTO_INCREMENT,
    `Sku`                    VARCHAR(64)   NOT NULL,
    `Name`                   VARCHAR(200)  NOT NULL,
    `Slug`                   VARCHAR(200)  NOT NULL,
    `ShortDescription`       VARCHAR(500)  NULL,
    `Description`            TEXT          NULL,
    -- ProductType: 0 = Plant, 1 = Produce
    `ProductType`            INT           NOT NULL,
    `CategoryId`             INT           NOT NULL,
    `IsActive`               TINYINT(1)    NOT NULL DEFAULT 1,
    `CreatedAt`              DATETIME(6)   NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    -- Plant-specific (NULL when ProductType = Produce)
    -- SunlightNeeds: 0 = FullSun, 1 = PartialSun, 2 = Shade
    `SunlightNeeds`          INT           NULL,
    `WateringFrequency`      VARCHAR(255)  NULL,
    `MatureHeightCm`         DECIMAL(6,2)  NULL,
    `PotSizeCm`              DECIMAL(6,2)  NULL,
    `IsIndoor`               TINYINT(1)    NULL,
    `IsPetSafe`              TINYINT(1)    NULL,
    -- Produce-specific (NULL when ProductType = Plant)
    -- UnitOfSale: 0 = PerItem, 1 = PerLb, 2 = PerKg, 3 = PerBunch, 4 = PerDozen
    `UnitOfSale`             INT           NULL,
    `IsOrganic`              TINYINT(1)    NULL,
    `IsSeasonal`             TINYINT(1)    NULL,
    `HarvestSeasonStartMonth` INT          NULL,
    `HarvestSeasonEndMonth`   INT          NULL,
    PRIMARY KEY (`Id`),
    UNIQUE KEY `IX_Products_Sku` (`Sku`),
    UNIQUE KEY `IX_Products_Slug` (`Slug`),
    KEY `IX_Products_CategoryId` (`CategoryId`),
    CONSTRAINT `FK_Products_Categories_CategoryId`
        FOREIGN KEY (`CategoryId`) REFERENCES `Categories` (`Id`) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='A single sellable item; region-specific price/stock lives in ProductRegionListings, not here.';

CREATE TABLE IF NOT EXISTS `ProductImages` (
    `Id`        INT           NOT NULL AUTO_INCREMENT,
    `ProductId` INT           NOT NULL,
    `Url`       VARCHAR(1000) NOT NULL,
    `SortOrder` INT           NOT NULL DEFAULT 0,
    `AltText`   VARCHAR(255)  NULL,
    PRIMARY KEY (`Id`),
    KEY `IX_ProductImages_ProductId` (`ProductId`),
    CONSTRAINT `FK_ProductImages_Products_ProductId`
        FOREIGN KEY (`ProductId`) REFERENCES `Products` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `ProductRegionListings` (
    `Id`               INT            NOT NULL AUTO_INCREMENT,
    `ProductId`        INT            NOT NULL,
    -- RegionCode: 0 = US, 1 = IN
    `Region`           INT            NOT NULL,
    -- CurrencyCode: 0 = USD, 1 = INR
    `Currency`         INT            NOT NULL,
    `Price`            DECIMAL(10,2)  NOT NULL,
    `CompareAtPrice`   DECIMAL(10,2)  NULL,
    `StockQuantity`    INT            NOT NULL DEFAULT 0,
    `IsAvailable`      TINYINT(1)     NOT NULL DEFAULT 1,
    PRIMARY KEY (`Id`),
    UNIQUE KEY `IX_ProductRegionListings_ProductId_Region` (`ProductId`, `Region`),
    CONSTRAINT `FK_ProductRegionListings_Products_ProductId`
        FOREIGN KEY (`ProductId`) REFERENCES `Products` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='Join point between the shared catalog and the US/India storefronts - a missing row means not offered in that region.';

-- =============================================================================
-- Identity-adjacent domain tables (HealingNaturalFarms.Domain.Entities.Identity)
-- =============================================================================

CREATE TABLE IF NOT EXISTS `Addresses` (
    `Id`         INT          NOT NULL AUTO_INCREMENT,
    `UserId`     INT          NOT NULL,
    `FullName`   VARCHAR(200) NOT NULL,
    `Line1`      VARCHAR(200) NOT NULL,
    `Line2`      VARCHAR(255) NULL,
    `City`       VARCHAR(100) NOT NULL,
    `State`      VARCHAR(100) NOT NULL,
    `PostalCode` VARCHAR(20)  NOT NULL,
    `Country`    VARCHAR(2)   NOT NULL COMMENT 'ISO 3166-1 alpha-2, e.g. US / IN',
    `Phone`      VARCHAR(32)  NULL,
    `IsDefault`  TINYINT(1)   NOT NULL DEFAULT 0,
    PRIMARY KEY (`Id`),
    KEY `IX_Addresses_UserId` (`UserId`),
    CONSTRAINT `FK_Addresses_Users_UserId`
        FOREIGN KEY (`UserId`) REFERENCES `Users` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `DeviceRegistrations` (
    `Id`           INT          NOT NULL AUTO_INCREMENT,
    `UserId`       INT          NULL,
    -- DevicePlatform: 0 = Android, 1 = iOS
    `Platform`     INT          NOT NULL,
    `PushToken`    VARCHAR(512) NOT NULL,
    `DeviceModel`  VARCHAR(255) NULL,
    `AppVersion`   VARCHAR(64)  NULL,
    `RegisteredAt` DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `LastSeenAt`   DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (`Id`),
    UNIQUE KEY `IX_DeviceRegistrations_PushToken` (`PushToken`),
    KEY `IX_DeviceRegistrations_UserId` (`UserId`),
    CONSTRAINT `FK_DeviceRegistrations_Users_UserId`
        FOREIGN KEY (`UserId`) REFERENCES `Users` (`Id`) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- =============================================================================
-- Cart (HealingNaturalFarms.Domain.Entities.Cart + CartConfiguration)
-- =============================================================================
-- Cart.UserId and Order.UserId are deliberately NOT foreign keys in the EF
-- model (see the doc comment on Address.UserId): guest carts/orders use
-- GuestId instead, and neither entity carries a User navigation property.
-- They're plain indexed columns here, matching the model exactly.
-- =============================================================================

CREATE TABLE IF NOT EXISTS `Carts` (
    `Id`        INT         NOT NULL AUTO_INCREMENT,
    `UserId`    INT         NULL,
    `GuestId`   VARCHAR(64) NULL,
    -- RegionCode: 0 = US, 1 = IN
    `Region`    INT         NOT NULL,
    `CreatedAt` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `UpdatedAt` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (`Id`),
    KEY `IX_Carts_GuestId` (`GuestId`),
    KEY `IX_Carts_UserId` (`UserId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='Belongs to a logged-in user (UserId) or a guest (GuestId) - exactly one is set. Pinned to one region for its lifetime.';

CREATE TABLE IF NOT EXISTS `CartItems` (
    `Id`                 INT           NOT NULL AUTO_INCREMENT,
    `CartId`             INT           NOT NULL,
    `ProductId`          INT           NOT NULL,
    `Quantity`           INT           NOT NULL,
    `UnitPriceSnapshot`  DECIMAL(10,2) NOT NULL,
    PRIMARY KEY (`Id`),
    UNIQUE KEY `IX_CartItems_CartId_ProductId` (`CartId`, `ProductId`),
    KEY `IX_CartItems_ProductId` (`ProductId`),
    CONSTRAINT `FK_CartItems_Carts_CartId`
        FOREIGN KEY (`CartId`) REFERENCES `Carts` (`Id`) ON DELETE CASCADE,
    -- RESTRICT rather than EF's convention-default CASCADE: a product should
    -- be retired via Products.IsActive, not hard-deleted while it's sitting
    -- in someone's cart.
    CONSTRAINT `FK_CartItems_Products_ProductId`
        FOREIGN KEY (`ProductId`) REFERENCES `Products` (`Id`) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- =============================================================================
-- Orders (HealingNaturalFarms.Domain.Entities.Order + OrderConfiguration)
-- =============================================================================

CREATE TABLE IF NOT EXISTS `Orders` (
    `Id`                 INT           NOT NULL AUTO_INCREMENT,
    `OrderNumber`        VARCHAR(32)   NOT NULL COMMENT 'Human-facing, e.g. HNF-US-000123',
    `UserId`             INT           NULL,
    -- RegionCode: 0 = US, 1 = IN
    `Region`             INT           NOT NULL,
    -- CurrencyCode: 0 = USD, 1 = INR
    `Currency`           INT           NOT NULL,
    `Subtotal`           DECIMAL(10,2) NOT NULL,
    `Tax`                DECIMAL(10,2) NOT NULL,
    `ShippingCost`       DECIMAL(10,2) NOT NULL,
    `Total`              DECIMAL(10,2) NOT NULL,
    -- OrderStatus: 0 = PendingPayment, 1 = Paid, 2 = Processing, 3 = Shipped,
    --              4 = Delivered, 5 = Cancelled, 6 = Refunded
    `Status`             INT           NOT NULL DEFAULT 0,
    `ShippingAddressId`  INT           NOT NULL,
    `ContactEmail`       VARCHAR(256)  NULL,
    `ContactPhone`       VARCHAR(32)   NULL,
    `CreatedAt`          DATETIME(6)   NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `UpdatedAt`          DATETIME(6)   NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (`Id`),
    UNIQUE KEY `IX_Orders_OrderNumber` (`OrderNumber`),
    KEY `IX_Orders_ShippingAddressId` (`ShippingAddressId`),
    CONSTRAINT `FK_Orders_Addresses_ShippingAddressId`
        FOREIGN KEY (`ShippingAddressId`) REFERENCES `Addresses` (`Id`) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `OrderItems` (
    `Id`                    INT           NOT NULL AUTO_INCREMENT,
    `OrderId`               INT           NOT NULL,
    `ProductId`             INT           NOT NULL,
    `ProductNameSnapshot`   VARCHAR(200)  NOT NULL,
    `UnitPrice`             DECIMAL(10,2) NOT NULL,
    `Quantity`              INT           NOT NULL,
    `LineTotal`             DECIMAL(10,2) NOT NULL,
    PRIMARY KEY (`Id`),
    KEY `IX_OrderItems_OrderId` (`OrderId`),
    KEY `IX_OrderItems_ProductId` (`ProductId`),
    CONSTRAINT `FK_OrderItems_Orders_OrderId`
        FOREIGN KEY (`OrderId`) REFERENCES `Orders` (`Id`) ON DELETE CASCADE,
    -- RESTRICT, same reasoning as CartItems: historical order lines must
    -- survive a product being taken out of the catalog.
    CONSTRAINT `FK_OrderItems_Products_ProductId`
        FOREIGN KEY (`ProductId`) REFERENCES `Products` (`Id`) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='Snapshots product name/price so historical orders stay accurate after a product is renamed or re-priced.';

CREATE TABLE IF NOT EXISTS `Payments` (
    `Id`                 INT           NOT NULL AUTO_INCREMENT,
    `OrderId`            INT           NOT NULL,
    -- PaymentProvider: 0 = Stripe, 1 = PayPal, 2 = Razorpay
    `Provider`           INT           NOT NULL,
    `ProviderReference`  VARCHAR(200)  NOT NULL COMMENT 'Stripe PaymentIntent id / PayPal order id / Razorpay payment id',
    `Amount`             DECIMAL(10,2) NOT NULL,
    -- CurrencyCode: 0 = USD, 1 = INR
    `Currency`           INT           NOT NULL,
    -- PaymentStatus: 0 = Pending, 1 = RequiresAction, 2 = Succeeded,
    --                3 = Failed, 4 = Refunded, 5 = PartiallyRefunded
    `Status`             INT           NOT NULL DEFAULT 0,
    `RawResponseJson`    LONGTEXT      NULL,
    `CreatedAt`          DATETIME(6)   NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `UpdatedAt`          DATETIME(6)   NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (`Id`),
    KEY `IX_Payments_OrderId` (`OrderId`),
    KEY `IX_Payments_ProviderReference` (`ProviderReference`),
    CONSTRAINT `FK_Payments_Orders_OrderId`
        FOREIGN KEY (`OrderId`) REFERENCES `Orders` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='One attempted or completed payment per row - an order can have more than one (e.g. a failed retry then a success).';

-- =============================================================================
-- Optional: application login used by the API's connection string
-- (ConnectionStrings:Default in appsettings.json - "User=hnf_app").
-- Uncomment and set a real password if this schema is being applied by a
-- MySQL admin account rather than by hnf_app itself.
-- =============================================================================
-- CREATE USER IF NOT EXISTS 'hnf_app'@'%' IDENTIFIED BY 'CHANGE_ME';
-- GRANT SELECT, INSERT, UPDATE, DELETE ON `hnf`.* TO 'hnf_app'@'%';
-- FLUSH PRIVILEGES;
