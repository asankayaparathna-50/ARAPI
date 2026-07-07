--- Stored Procedures for fetching data from Frequency tables
CREATE OR ALTER PROCEDURE SP_GetFrequencies
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        RTRIM(LTRIM(FrequencyCode)) AS FrequencyCode,
        NULLIF(RTRIM(LTRIM(Description)), '') AS Description,
        STS1
    FROM 
        Frequency
    ORDER BY 
        FrequencyCode;
END
GO

-- Stored Procedures for fetching data from Sector tables
CREATE OR ALTER PROCEDURE SP_GetSectors
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        RTRIM(LTRIM(SectorCode)) AS SectorCode,
        NULLIF(RTRIM(LTRIM(Name)), '') AS Name
    FROM 
        Sector
    ORDER BY 
        SectorCode;
END
GO
 -- Stored Procedures for fetching data from Subject tables
CREATE OR ALTER PROCEDURE SP_GetSubjects
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        Id,
        RTRIM(LTRIM(Name)) AS Name,
        RTRIM(LTRIM(SectorCode)) AS SectorCode,
        STS2
    FROM 
        Subject
    ORDER BY 
        Id;
END
GO

-- Stored Procedures for fetching data from Subject tables by SectorCode
CREATE OR ALTER PROCEDURE SP_GetSubjectsBySector
    @SectorCode NVARCHAR(50) -- Adjust the size based on your actual column size
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        Id,
        RTRIM(LTRIM(Name)) AS Name,
        RTRIM(LTRIM(SectorCode)) AS SectorCode,
        STS2
    FROM 
        Subject
    WHERE 
        RTRIM(LTRIM(SectorCode)) = @SectorCode
    ORDER BY 
        Id;
END
GO

-- Stored Procedures for fetching data from Topic tables by SubId
CREATE OR ALTER PROCEDURE SP_GetDataCodeList
    @SubId INT = NULL, 
    @SectorCode NVARCHAR(10) = NULL, 
    @FrequencyCode NVARCHAR(50) = NULL,
    @DataCodeID INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT 
        DataCodeID,
        [Item Name] as ItemName,
        [Short Name] as ShortName,
        Unit,
        [Scale],
        SectorCode,
        GeoAreaCode,
        FrequencyCode,
        SecurityLevel,
        [Source],
        Notes,
        DepartmentCode,
        DivisionId,
        KeyWords,
        Status,
        CreatedBy,
        CreatedOn,
        ModifiedBy,
        ModifiedOn,
        isQuarterStartApril,
        TopicId,
        DelayInWords
    FROM 
        DataCode
    WHERE
        (@SubId IS NULL OR TopicId IN (SELECT id FROM topic WHERE SubId = @SubId))
        AND (@DataCodeID IS NULL OR DataCodeID = @DataCodeID)
        AND (@SectorCode IS NULL OR SectorCode = @SectorCode)
        AND (@FrequencyCode IS NULL OR FrequencyCode = @FrequencyCode);
END
GO

-- Client table for storing API client credentials and permissions
CREATE TABLE API_Client (
    ClientId VARCHAR(50) NOT NULL PRIMARY KEY,
    ClientSecret VARCHAR(255) NOT NULL,
    RefreshedToken nvarchar(128) NULL,
    AllowedScopes VARCHAR(255) NULL, 
    IsActive BIT DEFAULT 1 NOT NULL,
    CreatedOn DATETIME DEFAULT GETDATE(),
    LastAccessedOn datetime NULL,
	SecretExpiresOn datetime NULL,
);
GO

-- Stored Procedure to find client by id
CREATE PROCEDURE SP_FindClientById
    @ClientId VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        ClientId,
        ClientSecret,
        AllowedScopes
    FROM 
       API_Client
    WHERE 
        ClientId = @ClientId
        AND IsActive = 1;
END
GO

-- Table to store application settings
CREATE TABLE ClientAppSetting  (
    [Key] VARCHAR(150) NOT NULL PRIMARY KEY,
    Value VARCHAR(MAX) NOT NULL,
    Description VARCHAR(500) NULL, 
    CreatedOn DATETIME DEFAULT GETDATE()
);
GO

-- Insert initial settings
INSERT INTO ClientAppSetting([Key],Value,Description) VALUES ('ex-bs-eur-DataCodeId','13184','Exchange Rates - Buying / Selling (Euro) - Rs')
GO
INSERT INTO ClientAppSetting([Key],Value,Description) VALUES ('ex-bs-usd-DataCodeId','14274','Exchange Rates - Buying / Selling (USD) - Rs')
GO


CREATE OR ALTER PROCEDURE SP_GetAppSettingValue
    @Key VARCHAR(150)
AS
BEGIN
    SELECT
        [Key],
        [Value],
        [Description]
    FROM
        ClientAppSetting
    WHERE
        [Key] = @Key;
END;
GO

CREATE OR ALTER PROCEDURE SP_GetDataValueByRange
    @DataCodeID INT,
    @FromPeriodID VARCHAR(10) = NULL,
    @ToPeriodID VARCHAR(10) = NULL
AS
BEGIN
    SET NOCOUNT ON; 

    SELECT
        DataCodeID,
        PeriodID,
        Value,
        FootNote,
        Status,
        CreatedBy,
        CreatedOn,
        ModifiedBy,
        ModifiedOn,
        ApprovedBy,
        ApprovedOn,
        FrequencyCode
    FROM
       DataValue
    WHERE
        DataCodeID = @DataCodeID
        AND (@FromPeriodID IS NULL OR PeriodID >= @FromPeriodID)
        AND (@ToPeriodID IS NULL OR PeriodID <= @ToPeriodID)

    ORDER BY
        PeriodID;
END;
GO


CREATE OR ALTER PROCEDURE SP_GetDataValuesByRange
    @DataCodeID INT,
    @FromPeriodID VARCHAR(10) = NULL,
    @ToPeriodID VARCHAR(10) = NULL,
    @FrequencyCode VARCHAR(10) = NULL
AS
BEGIN
    SET NOCOUNT ON; 

    SELECT
		t.Name AS 'TopicName'
		,s.Name AS 'SubjectName'
		,dc.Unit 
		,dc.[Scale]
		,dv.PeriodID
		,dv.Value
        ,dc.[Item Name] AS 'ItemName'
    FROM DataValue dv
    INNER JOIN DataCode dc ON dc.DataCodeID  = dv.DataCodeID  
    INNER JOIN Topic t ON dc.TopicId = t.Id 
    INNER JOIN Subject s ON s.Id = t.SubId 
    WHERE
        dv.DataCodeID = @DataCodeID
        AND dc.FrequencyCode = @FrequencyCode
        AND (@FromPeriodID IS NULL OR dv.PeriodID >= @FromPeriodID)
        AND (@ToPeriodID IS NULL OR dv.PeriodID <= @ToPeriodID)

    ORDER BY
        PeriodID;
END;
GO


CREATE TABLE RefreshTokens (
    -- Unique Identifier for the token record (Primary Key)
    Id INT IDENTITY(1,1) NOT NULL,
    CONSTRAINT PK_RefreshTokens PRIMARY KEY CLUSTERED (Id),

    -- The Client ID associated with this token (Foreign Key relationship is highly recommended)
    ClientId VARCHAR(50) NOT NULL,

    -- The token value, stored as a hash for security (Mandatory: Hash the token before storing in production)
    TokenHash NVARCHAR(128) NOT NULL, 

    -- Timestamp for when the token was generated and issued
    IssuedAt DATETIME2 NOT NULL,

    -- Timestamp for when the token naturally expires
    ExpiresAt DATETIME2 NOT NULL,

    -- Flag indicating if the token has been used or explicitly revoked
    IsRevoked BIT DEFAULT 0 NOT NULL, 

    -- Index for fast lookup by token value during the refresh process
    CONSTRAINT UQ_TokenHash UNIQUE (TokenHash)
);
GO

-- Create a non-clustered index on ClientId to speed up revocation lookups (e.g., SP_UpsertRefreshToken)
CREATE NONCLUSTERED INDEX IX_RefreshTokens_ClientId ON RefreshTokens (ClientId)
GO

-- Optional: Create a non-clustered index combining ClientId and IsRevoked for fast fetching of active tokens
CREATE NONCLUSTERED INDEX IX_RefreshTokens_ActiveByClient ON RefreshTokens (ClientId, IsRevoked)
WHERE (IsRevoked = 0)
GO



CREATE OR ALTER PROCEDURE SP_UpsertRefreshToken
    @ClientId NVARCHAR(50),
    @TokenValue NVARCHAR(128),  -- Corresponds to the TokenHash field in the C# model
    @ExpiresAt DATETIME2
AS
BEGIN
    -- Set to prevent the count of the number of rows affected from being returned
    SET NOCOUNT ON;

    -- 1. Revoke (soft-delete/invalidate) any existing, non-revoked refresh tokens 
    --    for this ClientId. This enforces "single-use" token rotation.
    UPDATE RefreshTokens
    SET 
        IsRevoked = 1
    WHERE 
        ClientId = @ClientId
        AND IsRevoked = 0;

    -- 2. Insert the new refresh token
    INSERT INTO RefreshTokens 
    (
        ClientId, 
        TokenHash, 
        IssuedAt, 
        ExpiresAt, 
        IsRevoked
    )
    VALUES
    (
        @ClientId,
        @TokenValue,
        GETUTCDATE(), -- Use UTC for consistency
        @ExpiresAt,
        0             -- Set IsRevoked to 0 (valid) upon creation
    );
END
GO


-- Stored Procedure to find client by id
CREATE OR ALTER PROCEDURE SP_RToenFindClientById
    @ClientId VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        ClientId,
        TokenHash,
        IssuedAt,
        ExpiresAt,
        IsRevoked
    FROM 
       RefreshTokens
    WHERE 
        ClientId = @ClientId
         AND IsRevoked = 0
END
GO




-- =========================================================
-- Stored Procedure for Price Data with Weekly 1.2
-- =========================================================
CREATE OR ALTER PROCEDURE SP_GetPrices_ByPeriod
    @CurrentPeriod NVARCHAR(20) = NULL, -- e.g. '2025-03-06' (optional, will auto-detect if NULL)
    @Market NVARCHAR(100) = NULL        -- Market filter (e.g., 'Pettah', ) - optional
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @yyyy INT, @mm INT, @dd INT;
    DECLARE @curDate DATE;
    DECLARE @latestPeriod NVARCHAR(20);

    -- If @CurrentPeriod is not provided, find the latest period from DataValue table
    IF @CurrentPeriod IS NULL
    BEGIN
        -- Find the latest period from DataValue table for price data
        SELECT TOP 1 @latestPeriod = dv.PeriodID
        FROM dbo.DataValue dv
        INNER JOIN dbo.DataCode dc ON dc.DataCodeID = dv.DataCodeID
        WHERE dv.PeriodID IS NOT NULL
          AND LEN(dv.PeriodID) >= 10  -- Ensure valid format 'YYYY-MM-DD'
          AND dv.PeriodID LIKE '____-__-__'  -- Basic format validation
          AND dc.FrequencyCode = 'D'  -- Daily data only
          AND (
	          	(@Market IS NULL OR dc.[Item Name] LIKE '%' + @Market + '-Wholesale%') OR
	          	(@Market IS NULL OR dc.[Item Name] LIKE '%' + @Market + '-Retail%')
          	)
        ORDER BY dv.PeriodID DESC;

        -- If no data found, raise error
        IF @latestPeriod IS NULL
        BEGIN
            RAISERROR('No price data found for the specified criteria', 16, 1);
            RETURN;
        END

        SET @CurrentPeriod = @latestPeriod;
    END

    -- Validate input format (expect 'YYYY-MM-DD')
    IF @CurrentPeriod IS NULL OR LEN(@CurrentPeriod) < 10
    BEGIN
        RAISERROR('Invalid @CurrentPeriod. Expected format: YYYY-MM-DD', 16, 1);
        RETURN;
    END

    -- Parse year, month, and day from the 'YYYY-MM-DD' format
    BEGIN TRY
        SET @yyyy = TRY_CAST(SUBSTRING(@CurrentPeriod,1,4) AS INT);
        SET @mm   = TRY_CAST(SUBSTRING(@CurrentPeriod,6,2) AS INT);
        SET @dd   = TRY_CAST(SUBSTRING(@CurrentPeriod,9,2) AS INT);
        IF @yyyy IS NULL OR @mm IS NULL OR @dd IS NULL
        BEGIN
            RAISERROR('Unable to parse date from @CurrentPeriod: %s. Expected format: YYYY-MM-DD', 16, 1, @CurrentPeriod);
            RETURN;
        END
    END TRY
    BEGIN CATCH
        RAISERROR('Error parsing @CurrentPeriod: %s', 16, 1, @CurrentPeriod);
        RETURN;
    END CATCH

    -- Build base date and compute current period
    SET @curDate = DATEFROMPARTS(@yyyy, @mm, @dd);

    DECLARE @p_this_week     NVARCHAR(20) = FORMAT(@curDate, 'yyyy-MM-dd');

    -- Return price data for current period only
    SELECT
    	dc.[Item Name] AS ItemName,
        dc.DataCodeID,
        @p_this_week AS PeriodID,
        TRY_CONVERT(DECIMAL(18,3), dv.Value) AS CurrentValue
    FROM dbo.DataCode dc
    LEFT JOIN dbo.DataValue dv
        ON dv.DataCodeID = dc.DataCodeID
        AND dv.PeriodID = @p_this_week
    WHERE dc.FrequencyCode = 'D' AND  -- Weekly data only
         (@Market IS NULL OR dc.[Item Name] LIKE '%' + @Market + '%')
        -- Market-specific item conditions
        AND (
            -- Pettah market items
            (LOWER(@Market) = 'pettah' AND dc.[Item Name] IN (
                'Rice-Pettah-Wholesale-Samba',
                'Rice-Pettah-Wholesale-Kekulu Red',
                'Vegetable-Pettah-Wholesale-Beans',
                'Vegetable-Pettah-Wholesale-Cabbage',
                'Vegetable-Pettah-Wholesale-Carrot',
                'Vegetable-Pettah-Wholesale-Tomatoes',
                'Vegetable-Pettah-Wholesale-Pumpkin',
                'Vegetable-Pettah-Wholesale-Snake gourd',
                'Vegetable-Pettah-Wholesale-Brinjal',
                'Other-Pettah-Wholesale-Green Chilli',
                'Other-Pettah-Wholesale-Lime',
                'Other-Pettah-Wholesale-Red Onions (local)',
                'Other-Pettah-Wholesale-Big Onions (local)',
                'Other-Pettah-Wholesale-Potatoes (local)',
                'Other-Pettah-Wholesale-Dried Chillies (imp)',
                'Other-Pettah-Wholesale-Dhal',
                'Other-Pettah-Wholesale-Eggs (White)',
                'Other-Pettah-Wholesale-Coconut',
                'Rice-Pettah-Retail-Samba',
                'Rice-Pettah-Retail-Kekulu Red',
                'Vegetable-Pettah-Retail-Beans',
                'Vegetable-Pettah-Retail-Cabbage',
                'Vegetable-Pettah-Retail-Carrot',
                'Vegetable-Pettah-Retail-Tomatoes',
                'Vegetable-Pettah-Retail-Pumpkin',
                'Vegetable-Pettah-Retail-Snake gourd',
                'Vegetable-Pettah-Retail-Brinjal',
                'Other-Pettah-Retail-Green Chilli',
                'Other-Pettah-Retail-Lime',
                'Other-Pettah-Retail-Red Onions (local)',
                'Other-Pettah-Retail-Big Onions (local)',
                'Other-Pettah-Retail-Potatoes (local)',
                'Other-Pettah-Retail-Dried Chillies (imp)',
                'Other-Pettah-Wholesale-Dhal',
                'Other-Pettah-Retail-Eggs (White)',
                'Other-Pettah-Retail-Coconut'
            )) OR
            
            -- Maradagahamula market items
            (LOWER(@Market) = 'maradagahamula' AND dc.[Item Name] IN (
                'Rice-Maradagahamula-Wholesale-Samba',
                'Rice-Maradagahamula-Wholesale-Kekulu White',
                'Vegetable-Maradagahamula-Wholesale-Kekulu Red',
                'Vegetable-Maradagahamula-Wholesale-Nadu'
            )) OR
            
            -- Dambulla market items
            (LOWER(@Market) = 'dambulla' AND dc.[Item Name] IN (
                'Rice-Dambulla-Wholesale-Samba',
                'Rice-Dambulla-Wholesale-Kekulu red',
                'Vegetable-Dambulla-Wholesale-Beans',
                'Vegetable-Dambulla-Wholesale-Cabbage',
                'Vegetable-Dambulla-Wholesale-Carrot',
                'Vegetable-Dambulla-Wholesale-Tomatoes',
                'Vegetable-Dambulla-Wholesale-Pumpkin',
                'Vegetable-Dambulla-Wholesale-Snake gourd',
                'Vegetable-Dambulla-Wholesale-Brinjal',
                'Vegetable-Dambulla-Wholesale-Ash Plantain',
                'Other-Dambulla-Wholesale-Red Onions (local)',
                'Other-Dambulla-Wholesale-Red Onions (imp)',
                'Other-Dambulla-Wholesale-Big Onions (imp)',
                'Other-Dambulla-Wholesale-Potatoes (local)',
                'Other-Dambulla-Wholesale-Potatoes (imp)',
                'Vegetable-Dambulla-Wholesale-Green Chilli',
                'Other-Dambulla-Wholesale-Coconut'
            )) OR

            -- Narahenpita Economic Centre items (fish, rice, vegetables, and other items)
            (LOWER(@Market) = 'narahenpita' AND dc.[Item Name] IN (
                'Rice-Narahenpita Economic Centre-Retail-Nadu',
                'Rice-Narahenpita Economic Centre-Retail-Kekulu Red',
                'Vegetable-Narahenpita Economic Centre-Retail-Beans',
                'Vegetable-Narahenpita Economic Centre-Retail-Cabbage',
                'Vegetable-Narahenpita Economic Centre-Retail-Carrot',
                'Vegetable-Narahenpita Economic Centre-Retail-Tomatoes',
                'Vegetable-Narahenpita Economic Centre-Retail-Pumpkin',
                'Vegetable-Narahenpita Economic Centre-Retail-Snake gourd',
                'Vegetable-Narahenpita Economic Centre-Retail-Brinjal',
                'Other-Narahenpita Economic Centre-Retail-Green Chilli',
                'Other-Narahenpita Economic Centre-Retail-Red Onions (local)',
                'Other-Narahenpita Economic Centre-Retail-Big Onion (Imp)',
                'Other-Narahenpita Economic Centre-Retail-Potato(local)',
                'Other-Narahenpita Economic Centre-Retail-Potato(Imp)',
                'Other-Narahenpita Economic Centre-Dried Chillies (imp)',
                'Other-Narahenpita Economic Centre-Retail-Dhal',
                'Other-Narahenpita Economic Centre-Retail-sugar(white)',
                'Other-Narahenpita Economic Centre-Retail-Eggs (White)',
                'Other-Narahenpita Economic Centre-Retail-Coconut'
            )) OR
            
            -- Fish market items
            -- Peliyagoda fish market items
            (LOWER(@Market) = 'fish-peliyagoda' AND dc.[Item Name] IN (
                'Fish-Peliyagoda-Wholesale-Kelawalla',
                'Fish-Peliyagoda-Wholesale-Balaya',
                'Fish-Peliyagoda-Wholesale-Salaya',
                'Fish-Peliyagoda-Wholesale-Hurulla'
            )) OR
            
            -- Negombo fish market items
            (LOWER(@Market) = 'fish-negombo' AND dc.[Item Name] IN (
                'Fish-Negombo-Wholesale-Kelawalla',
                'Fish-Negombo-Wholesale-Balaya',
                'Fish-Negombo-Wholesale-Salaya',
                'Fish-Negombo-Wholesale-Hurulla',
                'Fish-Negombo-Retail-Kelawalla',
                'Fish-Negombo-Retail-Balaya',
                'Fish-Negombo-Retail-Salaya',
                'Fish-Negombo-Retail-Hurulla'
            )) OR
            
            -- Narahenpita Economic Centre items (fish, rice, vegetables, and other items)
            (LOWER(@Market) = 'fish-narahenpita' AND dc.[Item Name] IN (
                'Fish-Narahenpita Economic Centre-Retail-Kelawalla',
                'Fish-Narahenpita Economic Centre-Retail-Balaya',
                'Fish-Narahenpita Economic Centre-Retail-Salaya',
                'Fish-Narahenpita Economic Centre-Retail-Hurulla'
            )) OR
            
            -- Default case: if market doesn't match specific conditions, use original logic
            (@Market IS NULL OR LOWER(@Market) NOT IN ('pettah', 'maradagahamula', 'dambulla', 'peliyagoda', 'negombo', 'narahenpita', 'fish-peliyagoda', 'fish-negombo', 'fish-narahenpita') )
        )
        AND dv.Value IS NOT NULL  -- Only show items with current week data
    ORDER BY ISNULL(dc.[Short Name], dc.[Item Name]);
END
GO;

-- =====================================================
-- Stored Procedure for GDP Growth Data 1.3
-- =====================================================
CREATE OR ALTER PROCEDURE SP_GetGdpGrowth_ByPeriod
    @CurrentPeriod NVARCHAR(20) = NULL,
    @FrequencyCode NVARCHAR(10) = NULL,
    @YearCode NVARCHAR(20) = NULL,
    @QuarterCode INT = NULL  
AS
BEGIN
    SET NOCOUNT ON;
    
    ---------------------DECLARE VARIABLES ----------------------
	DECLARE @inputDate DATE = NULL;
    DECLARE @year INT = NULL;
    DECLARE @month INT = NULL;
    DECLARE @date INT = NULL;
	-------------------------------------------------------------


    ---------------------CHECK CURRENT PERIOD--------------------
        IF @CurrentPeriod IS  NULL BEGIN 
            IF @YearCode IS  NOT NULL
                BEGIN SET @year = @YearCode; END
            ELSE
                BEGIN RETURN; END
        END
        ELSE BEGIN 
            SET @inputDate = CAST(@CurrentPeriod AS DATE);
            SET @year = YEAR(@inputDate);
            SET @month = MONTH(@inputDate);
            --SET @date = DATE(@inputDate);
        END
    -------------------------------------------------------------

    --------CHECK FREQUENCY IF @FrequencyCode = 'A'--------------
        
        IF @FrequencyCode IS NOT NULL AND @FrequencyCode = 'A'
        BEGIN
            SET @CurrentPeriod =  CAST(@year AS NVARCHAR(4)) + '-00-00';
        END
    -------------------------------------------------------------

    -----------CHECK FREQUENCY IF @FrequencyCode = 'Q'-----------
        IF @FrequencyCode IS NOT NULL AND @FrequencyCode = 'Q'
        BEGIN
            IF @QuarterCode IS  NULL
                BEGIN
                    SET @month = CASE 
                        WHEN MONTH(@inputDate) BETWEEN 1 AND 3 THEN 1
                        WHEN MONTH(@inputDate) BETWEEN 4 AND 6 THEN 2
                        WHEN MONTH(@inputDate) BETWEEN 7 AND 9 THEN 3
                        ELSE 4
                    END;
                    SET @CurrentPeriod =  CAST(@year AS NVARCHAR(4)) + '-Q' + CAST(@month AS NVARCHAR(1)) + '-00';
                END
            ELSE
                BEGIN SET @CurrentPeriod =  CAST(@year AS NVARCHAR(4)) + '-Q' + @QuarterCode + '-00'; END
        END
    -------------------------------------------------------------

    ----------------TABLE FOR DATA ITEMS NAME--------------------
        DECLARE @TargetItems TABLE (ItemName NVARCHAR(200));
        INSERT INTO @TargetItems (ItemName) VALUES
                ('GDP Growth - Constant (2015) Prices - Agriculture, Forestry and Fishing'),
                ('GDP Growth - Constant (2015) Prices - Industries'),
                ('GDP Growth - Constant (2015) Prices - Services'),
                ('GDP Growth - Constant (2015) Prices - Taxes less Subsidies on products'),
                ('GDP Growth - Constant (2015) Prices - Gross Domestic Product');
    -------------------------------------------------------------

    -----------------SELECT DATA FROM TABLE----------------------
        SELECT 
                CASE 
                    WHEN CHARINDEX('-', REVERSE(dc.[Item Name])) > 0 THEN LTRIM(RTRIM(REVERSE(SUBSTRING(REVERSE(dc.[Item Name]), 1, CHARINDEX('-', REVERSE(dc.[Item Name])) - 1))))
                    ELSE LTRIM(RTRIM(dc.[Item Name]))
                END AS 'Item',
                dc.[Item Name] AS 'ItemName',
                dc.DataCodeID,
                dv.PeriodId,
                dv.Value AS 'CurrentValue'
        FROM DataCode dc
        INNER JOIN DataValue dv ON dv.DataCodeID = dc.DataCodeID
        INNER JOIN @TargetItems ti ON ti.ItemName = dc.[Item Name]
        WHERE dv.PeriodID = @CurrentPeriod

END;
GO;

-- =====================================================
-- Stored Procedure for Agricultural Production Data 1.4
-- =====================================================
CREATE OR ALTER PROCEDURE SP_GetAgriculturalProduction_ByPeriod
    @CurrentPeriod NVARCHAR(20) = NULL,
    @FrequencyCode NVARCHAR(10) = NULL,
    @YearCode NVARCHAR(20) = NULL,
    @QuarterCode INT = NULL  
AS
BEGIN
    SET NOCOUNT ON;

	DECLARE @inputDate DATE = NULL;
    DECLARE @year INT = NULL;
    DECLARE @month INT = NULL;
    DECLARE @date INT = NULL;
	
    -------------------------------------------------------------
    ---------------------CHECK CURRENT PERIOD--------------------
        
        IF @CurrentPeriod IS  NULL BEGIN 
            IF @YearCode IS  NOT NULL
                BEGIN SET @year = @YearCode; END
            ELSE
                BEGIN RETURN; END
        END
        ELSE BEGIN 
            SET @inputDate = CAST(@CurrentPeriod AS DATE);
            SET @year = YEAR(@inputDate);
            SET @month = MONTH(@inputDate);
            SET @date = DAY(@inputDate);
        END
        
    -------------------------------------------------------------
    --------CHECK FREQUENCY IF @FrequencyCode -------------------
        
        IF @FrequencyCode IS NOT NULL AND @FrequencyCode = 'A'
        BEGIN
            SET @CurrentPeriod =  CAST(@year AS NVARCHAR(4)) + '-00-00';
        END

        IF @FrequencyCode IS NOT NULL AND @FrequencyCode = 'M'
        BEGIN
            SET @CurrentPeriod =  CAST(@year AS NVARCHAR(4)) + '-' + CAST(@month AS NVARCHAR(2)) + '-00';
        END

        IF @FrequencyCode IS NOT NULL AND @FrequencyCode = 'D'
        BEGIN
            SET @CurrentPeriod =  CAST(@year AS NVARCHAR(4)) + '-' + CAST(@month AS NVARCHAR(2)) + '-' + CAST(@date AS NVARCHAR(2));
        END
        
        IF @FrequencyCode IS NOT NULL AND @FrequencyCode = 'Q'
        BEGIN
            IF @QuarterCode IS  NULL
                BEGIN
                    SET @month = CASE 
                        WHEN MONTH(@inputDate) BETWEEN 1 AND 3 THEN 1
                        WHEN MONTH(@inputDate) BETWEEN 4 AND 6 THEN 2
                        WHEN MONTH(@inputDate) BETWEEN 7 AND 9 THEN 3
                        ELSE 4
                    END;
                    SET @CurrentPeriod =  CAST(@year AS NVARCHAR(4)) + '-Q' + CAST(@month AS NVARCHAR(1)) + '-00';
                END
            ELSE
                BEGIN SET @CurrentPeriod =  CAST(@year AS NVARCHAR(4)) + '-Q' + @QuarterCode + '-00'; END
        END
        
    -------------------------------------------------------------
    ----------------TABLE FOR DATA ITEMS NAME--------------------
        DECLARE @TargetItems TABLE (ItemName NVARCHAR(200));
        INSERT INTO @TargetItems (ItemName) VALUES
                ('Tea Production'),
                ('Rubber Production'),
                ('Coconut Production'),
                ('Tea Production (Y-o-Y growth, %)'),
                ('Rubber Production (Y-o-Y growth, %)'),
                ('Coconut Production (Y-o-Y growth, %)');

    -------------------------------------------------------------
    -----------------SELECT DATA FROM TABLE----------------------
        SELECT 
                CASE 
                    WHEN CHARINDEX('-', REVERSE(dc.[Item Name])) > 0 THEN LTRIM(RTRIM(REVERSE(SUBSTRING(REVERSE(dc.[Item Name]), 1, CHARINDEX('-', REVERSE(dc.[Item Name])) - 1))))
                    ELSE LTRIM(RTRIM(dc.[Item Name]))
                END AS 'Item',
                dc.[Item Name] AS 'ItemName',
                dc.DataCodeID,
                dv.PeriodId,
                dv.Value AS 'CurrentValue'
        FROM DataCode dc
        INNER JOIN DataValue dv ON dv.DataCodeID = dc.DataCodeID
        INNER JOIN @TargetItems ti ON ti.ItemName = dc.[Item Name]
        WHERE dv.PeriodID = @CurrentPeriod

END;
GO;

-- =====================================================
-- Stored Procedure for Industrial Production Data 1.5
-- =====================================================
CREATE OR ALTER PROCEDURE SP_GetIndustrialProduction_ByPeriod
    @CurrentPeriod NVARCHAR(20) = NULL,
    @FrequencyCode NVARCHAR(10) = NULL,
    @YearCode NVARCHAR(20) = NULL,
    @QuarterCode INT = NULL  
AS
BEGIN
    SET NOCOUNT ON;

	DECLARE @inputDate DATE = NULL;
    DECLARE @year INT = NULL;
    DECLARE @month INT = NULL;
    DECLARE @date INT = NULL;
	
    -------------------------------------------------------------
    ---------------------CHECK CURRENT PERIOD--------------------
        
        IF @CurrentPeriod IS  NULL BEGIN 
            IF @YearCode IS  NOT NULL
                BEGIN SET @year = @YearCode; END
            ELSE
                BEGIN RETURN; END
        END
        ELSE BEGIN 
            SET @inputDate = CAST(@CurrentPeriod AS DATE);
            SET @year = YEAR(@inputDate);
            SET @month = MONTH(@inputDate);
            SET @date = DAY(@inputDate);
        END
        
    -------------------------------------------------------------
    --------CHECK FREQUENCY IF @FrequencyCode -------------------
        
        IF @FrequencyCode IS NOT NULL AND @FrequencyCode = 'A'
        BEGIN
            SET @CurrentPeriod =  CAST(@year AS NVARCHAR(4)) + '-00-00';
        END

        IF @FrequencyCode IS NOT NULL AND @FrequencyCode = 'M'
        BEGIN
            SET @CurrentPeriod =  CAST(@year AS NVARCHAR(4)) + '-' + CAST(@month AS NVARCHAR(2)) + '-00';
        END

        IF @FrequencyCode IS NOT NULL AND @FrequencyCode = 'D'
        BEGIN
            SET @CurrentPeriod =  CAST(@year AS NVARCHAR(4)) + '-' + CAST(@month AS NVARCHAR(2)) + '-' + CAST(@date AS NVARCHAR(2));
        END
        
        IF @FrequencyCode IS NOT NULL AND @FrequencyCode = 'Q'
        BEGIN
            IF @QuarterCode IS  NULL
                BEGIN
                    SET @month = CASE 
                        WHEN MONTH(@inputDate) BETWEEN 1 AND 3 THEN 1
                        WHEN MONTH(@inputDate) BETWEEN 4 AND 6 THEN 2
                        WHEN MONTH(@inputDate) BETWEEN 7 AND 9 THEN 3
                        ELSE 4
                    END;
                    SET @CurrentPeriod =  CAST(@year AS NVARCHAR(4)) + '-Q' + CAST(@month AS NVARCHAR(1)) + '-00';
                END
            ELSE
                BEGIN SET @CurrentPeriod =  CAST(@year AS NVARCHAR(4)) + '-Q' + @QuarterCode + '-00'; END
        END
        
    -------------------------------------------------------------
    ----------------TABLE FOR DATA ITEMS NAME--------------------
        DECLARE @TargetItems TABLE (ItemName NVARCHAR(200));
        INSERT INTO @TargetItems (ItemName) VALUES
                ('Index of Industrial Production'),
                ('Food products'),
                ('Wearing apparel'),
                ('Other non-metallic mineral products'),
                ('Coke and refined petroleum products'),
                ('Rubber and plastic products'),
                ('Chemicals and chemical products'),
                ('Beverages'),
                ('Index of Industrial Production - Change'),
                ('Food products - Change'),
                ('Wearing apparel - Change'),
                ('Other non-metallic mineral products - Change'),
                ('Coke and refined petroleum products - Change'),
                ('Rubber and plastic products - Change'),
                ('Chemicals and chemical products - Change'),
                ('Beverages - Change');

    -------------------------------------------------------------
    -----------------SELECT DATA FROM TABLE----------------------
        SELECT 
                LTRIM(RTRIM(REVERSE(SUBSTRING(REVERSE(dc.[Item Name]), 1, CHARINDEX('-', REVERSE(dc.[Item Name])) - 1)))) AS 'Item',
                dc.[Item Name] AS 'ItemName',
                dc.DataCodeID,
                dv.PeriodId,
                dv.Value AS 'CurrentValue'
        FROM DataCode dc
        INNER JOIN DataValue dv ON dv.DataCodeID = dc.DataCodeID
        INNER JOIN @TargetItems ti ON ti.ItemName = dc.[Item Name]
        WHERE dv.PeriodID = @CurrentPeriod
END;
GO;

--================================================================
-- Stored Procedure for Purchasing Managers' Index (PMI) Data 1.6
--================================================================
CREATE OR ALTER PROCEDURE SP_GetPMI_ByPeriod
    @CurrentPeriod NVARCHAR(20) = NULL,
    @FrequencyCode NVARCHAR(10) = NULL,
    @YearCode NVARCHAR(20) = NULL,
    @QuarterCode INT = NULL  
AS
BEGIN
    SET NOCOUNT ON;
	---------------------DECLARE VARIABLES ----------------------
		DECLARE @inputDate DATE = NULL;
		DECLARE @year INT = NULL;
		DECLARE @month INT = NULL;
		DECLARE @date INT = NULL;
	-------------------------------------------------------------

	---------------------CHECK CURRENT PERIOD--------------------
		IF @CurrentPeriod IS  NULL BEGIN 
			IF @YearCode IS  NOT NULL
				BEGIN SET @year = @YearCode; END
			ELSE
				BEGIN RETURN; END
		END
		ELSE BEGIN 
			SET @inputDate = CAST(@CurrentPeriod AS DATE);
			SET @year = YEAR(@inputDate);
			SET @month = MONTH(@inputDate);
			SET @date = DAY(@inputDate);
		END
	-------------------------------------------------------------

	--------CHECK FREQUENCY IF @FrequencyCode -------------------
		
		IF @FrequencyCode IS NOT NULL AND @FrequencyCode = 'A'
		BEGIN
			SET @CurrentPeriod =  CAST(@year AS NVARCHAR(4)) + '-00-00';
		END

		IF @FrequencyCode IS NOT NULL AND @FrequencyCode = 'M'
		BEGIN
			SET @CurrentPeriod =  CAST(@year AS NVARCHAR(4)) + '-' + CAST(@month AS NVARCHAR(2)) + '-00';
		END

		IF @FrequencyCode IS NOT NULL AND @FrequencyCode = 'D'
		BEGIN
			SET @CurrentPeriod =  CAST(@year AS NVARCHAR(4)) + '-' + CAST(@month AS NVARCHAR(2)) + '-' + CAST(@date AS NVARCHAR(2));
		END
		
		IF @FrequencyCode IS NOT NULL AND @FrequencyCode = 'Q'
		BEGIN
			IF @QuarterCode IS  NULL
				BEGIN
					SET @month = CASE 
						WHEN MONTH(@inputDate) BETWEEN 1 AND 3 THEN 1
						WHEN MONTH(@inputDate) BETWEEN 4 AND 6 THEN 2
						WHEN MONTH(@inputDate) BETWEEN 7 AND 9 THEN 3
						ELSE 4
					END;
					SET @CurrentPeriod =  CAST(@year AS NVARCHAR(4)) + '-Q' + CAST(@month AS NVARCHAR(1)) + '-00';
				END
			ELSE
				BEGIN SET @CurrentPeriod =  CAST(@year AS NVARCHAR(4)) + '-Q' + @QuarterCode + '-00'; END
		END
	-------------------------------------------------------------

	----------------TABLE FOR DATA ITEMS NAME--------------------
		DECLARE @TargetItems TABLE (ItemName NVARCHAR(200));
		INSERT INTO @TargetItems (ItemName) VALUES
				('Purchasing Managers'' Index - Manufacturing'),
				('Purchasing Managers'' Index - Services (Business Activity Index)'),
				('Purchasing Managers'' Index – Construction (Total Activity Index)');

	-------------------------------------------------------------

	-----------------SELECT DATA FROM TABLE----------------------
		SELECT 
				--LTRIM(RTRIM(REVERSE(SUBSTRING(REVERSE(dc.[Item Name]), 1, CHARINDEX('-', REVERSE(dc.[Item Name])) - 1)))) AS 'Item',
				dc.[Item Name] AS 'ItemName',
				dc.DataCodeID,
				dv.PeriodId,
				dv.Value AS 'CurrentValue'
		FROM DataCode dc
		INNER JOIN DataValue dv ON dv.DataCodeID = dc.DataCodeID
		INNER JOIN @TargetItems ti ON ti.ItemName = dc.[Item Name]
		WHERE dv.PeriodID = @CurrentPeriod
	-------------------------------------------------------------
END;
GO;

-- =====================================================
-- Insert Employment data settings into ClientAppSetting
-- =====================================================
INSERT INTO ClientAppSetting([Key],Value,Description) VALUES 
('LabourForceParticipationRate','1516','Labour Force Participation Rate - Sri Lanka - Annual/Quarterly'),
('UnemploymentRate','1510','Unemployment Rate - Sri Lanka - Annual/Quarterly'),
('EmploymentAgriculture','1418','Employment in Agriculture - Percent of Total Employment'),
('EmploymentIndustry','1419','Employment in Industry - Percent of Total Employment'),
('EmploymentServices','1425','Employment in Services - Percent of Total Employment');
GO;


INSERT INTO ClientAppSetting([Key],Value,Description) VALUES 
('EmploymentAgriculture-Q','1441','Employment in Agriculture - Percent of Total Employment'),
('EmploymentIndustry-Q','1442','Employment in Industry - Percent of Total Employment'),
('EmploymentServices-Q','1448','Employment in Services - Percent of Total Employment');
GO;

-- 1.7 Employment Data (includes both employment indicators and sector data)
CREATE OR ALTER PROCEDURE SP_GetEmployment_ByPeriod
    @CurrentPeriod NVARCHAR(20) = NULL -- e.g. '2023-06-30' in yyyy-MM-dd format
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @inputDate DATE;
    DECLARE @year INT;
    DECLARE @quarter INT;
    
    -- Validate and parse the input date
    BEGIN TRY
        SET @inputDate = CAST(@CurrentPeriod AS DATE);
        SET @year = YEAR(@inputDate);
        
        -- Calculate quarter from the date
        SET @quarter = CASE 
            WHEN MONTH(@inputDate) BETWEEN 1 AND 3 THEN 1
            WHEN MONTH(@inputDate) BETWEEN 4 AND 6 THEN 2
            WHEN MONTH(@inputDate) BETWEEN 7 AND 9 THEN 3
            ELSE 4
        END;
    END TRY
    BEGIN CATCH
        RAISERROR('Invalid date format. Expected: yyyy-MM-dd (e.g., 2023-06-30)', 16, 1);
        RETURN;
    END CATCH

    -- Build database period formats
    -- Annual period for previous year (e.g., 2022-00-00)
    DECLARE @dbAnnualYearAgo NVARCHAR(20) = CAST((@year - 1) AS NVARCHAR(4)) + '-00-00';
    
    -- Quarterly periods for previous year and current year (e.g., 2022-Q2-00, 2023-Q2-00)
    DECLARE @dbQuarterlyYearAgo NVARCHAR(20) = CAST((@year - 1) AS NVARCHAR(4)) + '-Q' + CAST(@quarter AS NVARCHAR(1)) + '-00';
    DECLARE @dbQuarterlyThisYear NVARCHAR(20) = CAST(@year AS NVARCHAR(4)) + '-Q' + CAST(@quarter AS NVARCHAR(1)) + '-00';

    -- Result Set 1: Employment indicators (Labour Force Participation Rate, Unemployment Rate)
    SELECT
        CASE cas.[Key]
            WHEN 'LabourForceParticipationRate' THEN 'Labour Force Participation Rate'
            WHEN 'UnemploymentRate' THEN 'Unemployment Rate'
            ELSE cas.[Key]
        END AS Item,
        cas.[Description] AS ItemName,
        -- YearAgoValue: Annual data from previous year (2022-00-00)
        MAX(CASE WHEN dv.PeriodID = @dbAnnualYearAgo AND dv.FrequencyCode = 'A' THEN TRY_CONVERT(DECIMAL(18,1), dv.Value) END) AS YearAgoValue,
        -- YearAgoWithQuarter: Quarterly data from previous year (2022-Q2-00)
        MAX(CASE WHEN dv.PeriodID = @dbQuarterlyYearAgo AND dv.FrequencyCode = 'Q' THEN TRY_CONVERT(DECIMAL(18,1), dv.Value) END) AS YearAgoWithQuarter,
        -- ThisYearWithQuarter: Quarterly data from current year (2023-Q2-00)
        MAX(CASE WHEN dv.PeriodID = @dbQuarterlyThisYear AND dv.FrequencyCode = 'Q' THEN TRY_CONVERT(DECIMAL(18,1), dv.Value) END) AS ThisYearWithQuarter
    FROM dbo.ClientAppSetting cas
    INNER JOIN dbo.DataCode dc ON dc.DataCodeID = TRY_CAST(cas.Value AS INT)
    LEFT JOIN dbo.DataValue dv ON dv.DataCodeID = dc.DataCodeID
        AND (
            (dv.FrequencyCode = 'A' AND dv.PeriodID = @dbAnnualYearAgo)
            OR (dv.FrequencyCode = 'Q' AND dv.PeriodID IN (@dbQuarterlyYearAgo, @dbQuarterlyThisYear))
        )
    WHERE cas.[Key] IN (
        'LabourForceParticipationRate',
        'UnemploymentRate'
    )
    GROUP BY cas.[Key], cas.[Description], dc.DataCodeID
    HAVING MAX(CASE WHEN dv.PeriodID = @dbQuarterlyThisYear AND dv.FrequencyCode = 'Q' THEN dv.Value END) IS NOT NULL  -- Only show records with current quarter data
    ORDER BY 
        CASE cas.[Key]
            WHEN 'LabourForceParticipationRate' THEN 1
            WHEN 'UnemploymentRate' THEN 2
            ELSE 3
        END;

    -- Result Set 2: Employed persons by sector
    SELECT
        CASE 
            WHEN cas_annual.[Key] = 'EmploymentAgriculture' THEN 'Agriculture'
            WHEN cas_annual.[Key] = 'EmploymentIndustry' THEN 'Industry'
            WHEN cas_annual.[Key] = 'EmploymentServices' THEN 'Services'
            ELSE cas_annual.[Key]
        END AS Item,
        -- YearAgoValue: Annual data from previous year (e.g., 1418, 1419, 1425)
        MAX(CASE WHEN dv_annual.PeriodID = @dbAnnualYearAgo AND dv_annual.FrequencyCode = 'A' THEN TRY_CONVERT(DECIMAL(18,1), dv_annual.Value) END) AS YearAgoValue,
        -- YearAgoWithQuarter: Quarterly data from previous year (e.g., 1441, 1442, 1448 for Q2-00)
        MAX(CASE WHEN dv_quarterly.PeriodID = @dbQuarterlyYearAgo AND dv_quarterly.FrequencyCode = 'Q' THEN TRY_CONVERT(DECIMAL(18,1), dv_quarterly.Value) END) AS YearAgoWithQuarter,
        -- ThisYearWithQuarter: Quarterly data from current year (e.g., 1441, 1442, 1448 for Q2-00)
        MAX(CASE WHEN dv_quarterly.PeriodID = @dbQuarterlyThisYear AND dv_quarterly.FrequencyCode = 'Q' THEN TRY_CONVERT(DECIMAL(18,1), dv_quarterly.Value) END) AS ThisYearWithQuarter
    FROM dbo.ClientAppSetting cas_annual
    INNER JOIN dbo.DataCode dc_annual ON dc_annual.DataCodeID = TRY_CAST(cas_annual.Value AS INT)
    LEFT JOIN dbo.DataValue dv_annual ON dv_annual.DataCodeID = dc_annual.DataCodeID
        AND dv_annual.FrequencyCode = 'A'
        AND dv_annual.PeriodID = @dbAnnualYearAgo
    -- Join quarterly data using the -Q suffix pattern
    LEFT JOIN dbo.ClientAppSetting cas_quarterly ON cas_quarterly.[Key] = cas_annual.[Key] + '-Q'
    LEFT JOIN dbo.DataCode dc_quarterly ON dc_quarterly.DataCodeID = TRY_CAST(cas_quarterly.Value AS INT)
    LEFT JOIN dbo.DataValue dv_quarterly ON dv_quarterly.DataCodeID = dc_quarterly.DataCodeID
        AND dv_quarterly.FrequencyCode = 'Q'
        AND dv_quarterly.PeriodID IN (@dbQuarterlyYearAgo, @dbQuarterlyThisYear)
    WHERE cas_annual.[Key] IN (
        'EmploymentAgriculture',
        'EmploymentIndustry',
        'EmploymentServices'
    )
    GROUP BY cas_annual.[Key], dc_annual.DataCodeID
    HAVING MAX(CASE WHEN dv_quarterly.PeriodID = @dbQuarterlyThisYear AND dv_quarterly.FrequencyCode = 'Q' THEN dv_quarterly.Value END) IS NOT NULL  -- Only show records with current quarter data
    ORDER BY 
        CASE cas_annual.[Key]
            WHEN 'EmploymentAgriculture' THEN 1
            WHEN 'EmploymentIndustry' THEN 2
            WHEN 'EmploymentServices' THEN 3
            ELSE 4
        END;
END;
GO;

-- 1.8 Employed Persons by Sector
CREATE OR ALTER PROCEDURE SP_GetEmployedPersonBySector_ByPeriod
    @CurrentPeriod NVARCHAR(20) = NULL -- e.g. '2024-09' in yyyy-MM format
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @year INT;
    DECLARE @month INT;
    DECLARE @curDate DATE;
    
    -- Validate and parse the input period
    BEGIN TRY
        SET @year = TRY_CAST(SUBSTRING(@CurrentPeriod, 1, 4) AS INT);
        SET @month = TRY_CAST(SUBSTRING(@CurrentPeriod, 6, 2) AS INT);
        
        IF @year IS NULL OR @month IS NULL OR @month < 1 OR @month > 12
        BEGIN
            RAISERROR('Unable to parse period. Expected format: yyyy-MM', 16, 1);
            RETURN;
        END
        
        SET @curDate = DATEFROMPARTS(@year, @month, 1);
    END TRY
    BEGIN CATCH
        RAISERROR('Invalid period format. Expected: yyyy-MM (e.g., 2024-09)', 16, 1);
        RETURN;
    END CATCH

    -- Build database period formats based on the provided period
    -- For employed persons by sector data, we use fixed periods: 2024 annual, 2025-Q1, 2025-Q2
    DECLARE @db2024Annual NVARCHAR(20) = '2024-00-00';
    DECLARE @db2025Q1 NVARCHAR(20) = '2025-Q1-00';
    DECLARE @db2025Q2 NVARCHAR(20) = '2025-Q2-00';

    -- Return employed persons by sector data (as percentage of total employment)
    SELECT
        CASE cas.[Key]
            WHEN 'EmploymentAgriculture' THEN 'Agriculture'
            WHEN 'EmploymentIndustry' THEN 'Industry'
            WHEN 'EmploymentServices' THEN 'Services'
            ELSE cas.[Key]
        END AS Sector,
        MAX(CASE WHEN dv.PeriodID = @db2024Annual THEN TRY_CONVERT(DECIMAL(18,1), dv.Value) END) AS Year2024,
        MAX(CASE WHEN dv.PeriodID = @db2025Q1 THEN TRY_CONVERT(DECIMAL(18,1), dv.Value) END) AS Year2025Q1,
        MAX(CASE WHEN dv.PeriodID = @db2025Q2 THEN TRY_CONVERT(DECIMAL(18,1), dv.Value) END) AS Year2025Q2
    FROM dbo.ClientAppSetting cas
    INNER JOIN dbo.DataCode dc ON dc.DataCodeID = TRY_CAST(cas.Value AS INT)
    LEFT JOIN dbo.DataValue dv ON dv.DataCodeID = dc.DataCodeID
        AND dv.PeriodID IN (@db2024Annual, @db2025Q1, @db2025Q2)
        AND dv.FrequencyCode IN ('A', 'Q')
    WHERE cas.[Key] IN (
        'EmploymentAgriculture',
        'EmploymentIndustry',
        'EmploymentServices'
    )
    GROUP BY cas.[Key], dc.DataCodeID
    HAVING MAX(CASE WHEN dv.PeriodID = @db2025Q2 THEN dv.Value END) IS NOT NULL
    ORDER BY 
        CASE cas.[Key]
            WHEN 'EmploymentAgriculture' THEN 1
            WHEN 'EmploymentIndustry' THEN 2
            WHEN 'EmploymentServices' THEN 3
            ELSE 4
        END;
END;
GO;

-- =====================================================
-- Insert Crude Oil Prices data settings into ClientAppSetting
-- =====================================================
INSERT INTO ClientAppSetting([Key],Value,Description) VALUES 
('CrudeOilBrent','13160','Crude Oil Futures Prices - Brent (Benchmark price)'),
('CrudeOilWTI','13161','Crude Oil Futures Prices - WTI (Benchmark price)'),
('CrudeOilCPCImport','13162','CPC Import Prices');
GO;

INSERT INTO ClientAppSetting([Key],Value,Description) VALUES 
('CrudeOilBrent-D','13158','Crude Oil Futures Prices - Brent (Benchmark price)'),
('CrudeOilWTI-D','13159','Crude Oil Futures Prices - WTI (Benchmark price)');
GO;

-- 1.9 Average Crude Oil Prices
CREATE OR ALTER PROCEDURE SP_GetCrudeOilPrices_ByPeriod
    @CurrentPeriod NVARCHAR(20) = NULL -- e.g. '2025-11-18' in yyyy-MM-dd format
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @inputDate DATE;
    DECLARE @year INT;
    DECLARE @previousYear INT;
    
    -- Validate and parse the input date
    BEGIN TRY
        SET @inputDate = CAST(@CurrentPeriod AS DATE);
        SET @year = YEAR(@inputDate);
        SET @previousYear = @year - 1;
    END TRY
    BEGIN CATCH
        RAISERROR('Invalid date format. Expected: yyyy-MM-dd (e.g., 2025-11-18)', 16, 1);
        RETURN;
    END CATCH

    -- Result Set 1: Monthly crude oil prices for all 12 months of both years (previous year and current year)
    SELECT
        CASE CAST(SUBSTRING(dv.PeriodID, 6, 2) AS INT)
            WHEN 1 THEN 'January'
            WHEN 2 THEN 'February'
            WHEN 3 THEN 'March'
            WHEN 4 THEN 'April'
            WHEN 5 THEN 'May'
            WHEN 6 THEN 'June'
            WHEN 7 THEN 'July'
            WHEN 8 THEN 'August'
            WHEN 9 THEN 'September'
            WHEN 10 THEN 'October'
            WHEN 11 THEN 'November'
            WHEN 12 THEN 'December'
        END AS MonthName,
        SUBSTRING(dv.PeriodID, 1, 4) AS Year,
        ISNULL(CAST(MAX(CASE WHEN cas.[Key] = 'CrudeOilBrent' THEN TRY_CONVERT(DECIMAL(18,2), dv.Value) END) AS NVARCHAR(20)), '-') AS CrudeOilFuturesPricesBrent,
        ISNULL(CAST(MAX(CASE WHEN cas.[Key] = 'CrudeOilWTI' THEN TRY_CONVERT(DECIMAL(18,2), dv.Value) END) AS NVARCHAR(20)), '-') AS CrudeOilFuturesPricesWTI,
        ISNULL(CAST(MAX(CASE WHEN cas.[Key] = 'CrudeOilCPCImport' THEN TRY_CONVERT(DECIMAL(18,2), dv.Value) END) AS NVARCHAR(20)), '-') AS CPCImportPrices
    FROM dbo.ClientAppSetting cas
    INNER JOIN dbo.DataCode dc ON dc.DataCodeID = TRY_CAST(cas.Value AS INT)
    INNER JOIN dbo.DataValue dv ON dv.DataCodeID = dc.DataCodeID
        AND dv.FrequencyCode = 'M'
        AND SUBSTRING(dv.PeriodID, 1, 4) IN (CAST(@previousYear AS NVARCHAR(4)), CAST(@year AS NVARCHAR(4)))
        AND dv.PeriodID LIKE '%-__-00'  -- Monthly data format yyyy-MM-00
    WHERE cas.[Key] IN (
        'CrudeOilBrent',
        'CrudeOilWTI',
        'CrudeOilCPCImport'
    )
    GROUP BY dv.PeriodID
    ORDER BY 
        SUBSTRING(dv.PeriodID, 1, 4),  -- Year
        CAST(SUBSTRING(dv.PeriodID, 6, 2) AS INT);  -- Month number

    -- Result Set 2: Daily crude oil prices for last 7 days (Brent and WTI only)
    -- Generate last 7 days based on input date
    DECLARE @day1 DATE = DATEADD(DAY, -6, @inputDate);
    DECLARE @day2 DATE = DATEADD(DAY, -5, @inputDate);
    DECLARE @day3 DATE = DATEADD(DAY, -4, @inputDate);
    DECLARE @day4 DATE = DATEADD(DAY, -3, @inputDate);
    DECLARE @day5 DATE = DATEADD(DAY, -2, @inputDate);
    DECLARE @day6 DATE = DATEADD(DAY, -1, @inputDate);
    DECLARE @day7 DATE = @inputDate;

    -- Also get same days from previous year
    DECLARE @day1PrevYear DATE = DATEADD(YEAR, -1, @day1);
    DECLARE @day2PrevYear DATE = DATEADD(YEAR, -1, @day2);
    DECLARE @day3PrevYear DATE = DATEADD(YEAR, -1, @day3);
    DECLARE @day4PrevYear DATE = DATEADD(YEAR, -1, @day4);
    DECLARE @day5PrevYear DATE = DATEADD(YEAR, -1, @day5);
    DECLARE @day6PrevYear DATE = DATEADD(YEAR, -1, @day6);
    DECLARE @day7PrevYear DATE = DATEADD(YEAR, -1, @day7);

    SELECT
        FORMAT(CAST(dv.PeriodID AS DATE), 'dd-MMM') AS DayLabel,
        YEAR(CAST(dv.PeriodID AS DATE)) AS Year,
        ISNULL(CAST(MAX(CASE WHEN cas.[Key] = 'CrudeOilBrent-D' THEN TRY_CONVERT(DECIMAL(18,2), dv.Value) END) AS NVARCHAR(20)), '-') AS CrudeOilFuturesPricesBrent,
        ISNULL(CAST(MAX(CASE WHEN cas.[Key] = 'CrudeOilWTI-D' THEN TRY_CONVERT(DECIMAL(18,2), dv.Value) END) AS NVARCHAR(20)), '-') AS CrudeOilFuturesPricesWTI
    FROM dbo.ClientAppSetting cas
    INNER JOIN dbo.DataCode dc ON dc.DataCodeID = TRY_CAST(cas.Value AS INT)
    INNER JOIN dbo.DataValue dv ON dv.DataCodeID = dc.DataCodeID
        AND dv.FrequencyCode = 'D'
        AND CAST(dv.PeriodID AS DATE) IN (
            @day1, @day2, @day3, @day4, @day5, @day6, @day7,
            @day1PrevYear, @day2PrevYear, @day3PrevYear, @day4PrevYear, @day5PrevYear, @day6PrevYear, @day7PrevYear
        )
    WHERE cas.[Key] IN (
        'CrudeOilBrent-D',
        'CrudeOilWTI-D'
    )
    GROUP BY dv.PeriodID
    ORDER BY 
        FORMAT(CAST(dv.PeriodID AS DATE), 'dd-MMM'),  -- Order by day label first
        YEAR(CAST(dv.PeriodID AS DATE));  -- Then by year
END;
GO;

-- 1.10 Daily Electricity Generation
-- Insert ClientAppSetting entries for Daily Electricity Generation
IF NOT EXISTS (SELECT 1 FROM ClientAppSetting WHERE [Key] = 'ElectricityGeneration-PeakDemand')
    INSERT INTO ClientAppSetting ([Key], Value, Description)
    VALUES ('ElectricityGeneration-PeakDemand', 13068, 'Electricity Generation - Peak Demand (MW)');
GO;

IF NOT EXISTS (SELECT 1 FROM ClientAppSetting WHERE [Key] = 'ElectricityGeneration-TotalEnergy')
    INSERT INTO ClientAppSetting ([Key], Value, Description)
    VALUES ('ElectricityGeneration-TotalEnergy', 13067, 'Electricity Generation - Total Energy (GWh)');
GO;

IF NOT EXISTS (SELECT 1 FROM ClientAppSetting WHERE [Key] = 'ElectricityGeneration-Hydro')
    INSERT INTO ClientAppSetting ([Key], Value, Description)
    VALUES ('ElectricityGeneration-Hydro', 13071, 'Electricity Generation - Hydro (GWh)');
GO;
IF NOT EXISTS (SELECT 1 FROM ClientAppSetting WHERE [Key] = 'ElectricityGeneration-ThermalCoal')
    INSERT INTO ClientAppSetting ([Key], Value, Description)
    VALUES ('ElectricityGeneration-ThermalCoal', 13069, 'Electricity Generation - Thermal Coal (GWh)');
GO;

IF NOT EXISTS (SELECT 1 FROM ClientAppSetting WHERE [Key] = 'ElectricityGeneration-ThermalOil')
    INSERT INTO ClientAppSetting ([Key], Value, Description)
    VALUES ('ElectricityGeneration-ThermalOil', 13070, 'Electricity Generation - Thermal Oil (GWh)');
GO;
IF NOT EXISTS (SELECT 1 FROM ClientAppSetting WHERE [Key] = 'ElectricityGeneration-Wind')
    INSERT INTO ClientAppSetting ([Key], Value, Description)
    VALUES ('ElectricityGeneration-Wind', 13072, 'Electricity Generation - Wind (GWh)');
GO;

IF NOT EXISTS (SELECT 1 FROM ClientAppSetting WHERE [Key] = 'ElectricityGeneration-Solar')
    INSERT INTO ClientAppSetting ([Key], Value, Description)
    VALUES ('ElectricityGeneration-Solar', 13073, 'Electricity Generation - Solar (GWh)');
GO;

IF NOT EXISTS (SELECT 1 FROM ClientAppSetting WHERE [Key] = 'ElectricityGeneration-Biomass')
    INSERT INTO ClientAppSetting ([Key], Value, Description)
    VALUES ('ElectricityGeneration-Biomass', 14229, 'Electricity Generation - Biomass (GWh)');
Go;

CREATE OR ALTER PROCEDURE SP_GetDailyElectricityGeneration
    @CurrentPeriod DATE
AS
BEGIN
    SET NOCOUNT ON;

    -- Calculate the last 5 days from the current period
    DECLARE @day1 DATE = @CurrentPeriod;
    DECLARE @day2 DATE = DATEADD(DAY, -1, @CurrentPeriod);
    DECLARE @day3 DATE = DATEADD(DAY, -2, @CurrentPeriod);
    DECLARE @day4 DATE = DATEADD(DAY, -3, @CurrentPeriod);
    DECLARE @day5 DATE = DATEADD(DAY, -4, @CurrentPeriod);

    -- Return data for last 5 days
    SELECT
        FORMAT(CAST(dv.PeriodID AS DATE), 'dd-MMM-yy') AS DayLabel,
        ISNULL(CAST(MAX(CASE WHEN cas.[Key] = 'ElectricityGeneration-PeakDemand' THEN TRY_CONVERT(DECIMAL(18,2), dv.Value) END) AS NVARCHAR(20)), '-') AS PeakDemandMW,
        ISNULL(CAST(MAX(CASE WHEN cas.[Key] = 'ElectricityGeneration-TotalEnergy' THEN TRY_CONVERT(DECIMAL(18,2), dv.Value) END) AS NVARCHAR(20)), '-') AS TotalEnergyGWh,
        ISNULL(CAST(MAX(CASE WHEN cas.[Key] = 'ElectricityGeneration-Hydro' THEN TRY_CONVERT(DECIMAL(18,2), dv.Value) END) AS NVARCHAR(20)), '-') AS HydroGWh,
        ISNULL(CAST(MAX(CASE WHEN cas.[Key] = 'ElectricityGeneration-ThermalCoal' THEN TRY_CONVERT(DECIMAL(18,2), dv.Value) END) AS NVARCHAR(20)), '-') AS ThermalCoalGWh,
        ISNULL(CAST(MAX(CASE WHEN cas.[Key] = 'ElectricityGeneration-ThermalOil' THEN TRY_CONVERT(DECIMAL(18,2), dv.Value) END) AS NVARCHAR(20)), '-') AS ThermalOilGWh,
        ISNULL(CAST(MAX(CASE WHEN cas.[Key] = 'ElectricityGeneration-Wind' THEN TRY_CONVERT(DECIMAL(18,2), dv.Value) END) AS NVARCHAR(20)), '-') AS WindGWh,
        ISNULL(CAST(MAX(CASE WHEN cas.[Key] = 'ElectricityGeneration-Solar' THEN TRY_CONVERT(DECIMAL(18,2), dv.Value) END) AS NVARCHAR(20)), '-') AS SolarGWh,
        ISNULL(CAST(MAX(CASE WHEN cas.[Key] = 'ElectricityGeneration-Biomass' THEN TRY_CONVERT(DECIMAL(18,2), dv.Value) END) AS NVARCHAR(20)), '-') AS BiomassGWh
    FROM dbo.ClientAppSetting cas
    INNER JOIN dbo.DataCode dc ON dc.DataCodeID = cas.value
    INNER JOIN dbo.DataValue dv ON dv.DataCodeID = dc.DataCodeID
        AND dv.FrequencyCode = 'D'
        AND CAST(dv.PeriodID AS DATE) IN (@day1, @day2, @day3, @day4, @day5)
    WHERE cas.[Key] IN (
        'ElectricityGeneration-PeakDemand',
        'ElectricityGeneration-TotalEnergy',
        'ElectricityGeneration-Hydro',
        'ElectricityGeneration-ThermalCoal',
        'ElectricityGeneration-ThermalOil',
        'ElectricityGeneration-Wind',
        'ElectricityGeneration-Solar',
        'ElectricityGeneration-Biomass'
    )
    GROUP BY dv.PeriodID
    ORDER BY CAST(dv.PeriodID AS DATE) DESC;  -- Most recent first
END;
GO;

-- =====================================================
-- Stored Procedure for Policy Interest Rate (OPR) Data
-- =====================================================

-- Insert Policy Interest Rate (OPR) data settings into ClientAppSetting
IF NOT EXISTS (SELECT 1 FROM ClientAppSetting WHERE [Key] = 'Policy Rates - Overnight Policy Rate (OPR)')
    INSERT INTO ClientAppSetting ([Key], Value, Description)
    VALUES ('Policy Rates - Overnight Policy Rate (OPR)', 14343, 'Policy Interest Rate - OPR (%)');
GO;

-- 2.1.1 Policy Interest Rate
CREATE OR ALTER PROCEDURE SP_GetPolicyInterestRate_ByPeriod
    @CurrentPeriod NVARCHAR(20) -- e.g. '2024-12-17' for daily or '2024-12-00' for monthly format
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @CurrentDate DATE;
    DECLARE @WeekAgoDate DATE;
    DECLARE @YearAgoDate DATE;
    
    -- Try to parse the period as a date
    SET @CurrentDate = TRY_CAST(@CurrentPeriod AS DATE);
    SET @WeekAgoDate = DATEADD(DAY, -7, @CurrentDate);
    SET @YearAgoDate = DATEADD(YEAR, -1, @CurrentDate);

    -- Policy Rates - OPR
    SELECT
        'Policy Rates - Overnight Policy Rate (OPR)' AS ItemName,
        ISNULL(TRY_CONVERT(DECIMAL(18,2), yearAgo.Value), 0) AS YearAgoValue,
        ISNULL(TRY_CONVERT(DECIMAL(18,2), weekAgo.Value), 0) AS WeekAgoValue,
        ISNULL(TRY_CONVERT(DECIMAL(18,2), thisWeek.Value), 0) AS ThisWeek
    FROM (SELECT 1 AS DummyKey) dummy
    LEFT JOIN (
        SELECT dv.Value
        FROM DataValue dv
        INNER JOIN DataCode dc ON dc.DataCodeID = dv.DataCodeID
        WHERE dc.[Item Name] = 'Policy Rates - Overnight Policy Rate (OPR)'
            AND dv.PeriodID = @CurrentPeriod
    ) thisWeek ON 1=1
    LEFT JOIN (
        SELECT TOP 1 dv.Value
        FROM DataValue dv
        INNER JOIN DataCode dc ON dc.DataCodeID = dv.DataCodeID
        WHERE dc.[Item Name] = 'Policy Rates - Overnight Policy Rate (OPR)'
            AND TRY_CAST(dv.PeriodID AS DATE) <= @WeekAgoDate
        ORDER BY TRY_CAST(dv.PeriodID AS DATE) DESC
    ) weekAgo ON 1=1
    LEFT JOIN (
        SELECT TOP 1 dv.Value
        FROM DataValue dv
        INNER JOIN DataCode dc ON dc.DataCodeID = dv.DataCodeID
        WHERE dc.[Item Name] = 'Policy Rates - Overnight Policy Rate (OPR)'
            AND TRY_CAST(dv.PeriodID AS DATE) <= @YearAgoDate
        ORDER BY TRY_CAST(dv.PeriodID AS DATE) DESC
    ) yearAgo ON 1=1

    UNION ALL

    -- Standing Deposit Facility Rate (SDFR)
    SELECT
        'Repo/Standing Deposit Facility - Rate' AS ItemName,
        ISNULL(TRY_CONVERT(DECIMAL(18,2), yearAgo.Value), 0) AS YearAgoValue,
        ISNULL(TRY_CONVERT(DECIMAL(18,2), weekAgo.Value), 0) AS WeekAgoValue,
        ISNULL(TRY_CONVERT(DECIMAL(18,2), thisWeek.Value), 0) AS ThisWeek
    FROM (SELECT 1 AS DummyKey) dummy
    LEFT JOIN (
        SELECT dv.Value
        FROM DataValue dv
        INNER JOIN DataCode dc ON dc.DataCodeID = dv.DataCodeID
        WHERE dc.[Item Name] = 'Repo/Standing Deposit Facility - Rate'
            AND dv.PeriodID = @CurrentPeriod
    ) thisWeek ON 1=1
    LEFT JOIN (
        SELECT TOP 1 dv.Value
        FROM DataValue dv
        INNER JOIN DataCode dc ON dc.DataCodeID = dv.DataCodeID
        WHERE dc.[Item Name] = 'Repo/Standing Deposit Facility - Rate'
            AND TRY_CAST(dv.PeriodID AS DATE) <= @WeekAgoDate
        ORDER BY TRY_CAST(dv.PeriodID AS DATE) DESC
    ) weekAgo ON 1=1
    LEFT JOIN (
        SELECT TOP 1 dv.Value
        FROM DataValue dv
        INNER JOIN DataCode dc ON dc.DataCodeID = dv.DataCodeID
        WHERE dc.[Item Name] = 'Repo/Standing Deposit Facility - Rate'
            AND TRY_CAST(dv.PeriodID AS DATE) <= @YearAgoDate
        ORDER BY TRY_CAST(dv.PeriodID AS DATE) DESC
    ) yearAgo ON 1=1

    UNION ALL

    -- Standing Lending Facility Rate (SLFR)
    SELECT
        'Reverse Repo/Standing Lending Facility - Rate' AS ItemName,
        ISNULL(TRY_CONVERT(DECIMAL(18,2), yearAgo.Value), 0) AS YearAgoValue,
        ISNULL(TRY_CONVERT(DECIMAL(18,2), weekAgo.Value), 0) AS WeekAgoValue,
        ISNULL(TRY_CONVERT(DECIMAL(18,2), thisWeek.Value), 0) AS ThisWeek
    FROM (SELECT 1 AS DummyKey) dummy
    LEFT JOIN (
        SELECT dv.Value
        FROM DataValue dv
        INNER JOIN DataCode dc ON dc.DataCodeID = dv.DataCodeID
        WHERE dc.[Item Name] = 'Reverse Repo/Standing Lending Facility - Rate'
            AND dv.PeriodID = @CurrentPeriod
    ) thisWeek ON 1=1
    LEFT JOIN (
        SELECT TOP 1 dv.Value
        FROM DataValue dv
        INNER JOIN DataCode dc ON dc.DataCodeID = dv.DataCodeID
        WHERE dc.[Item Name] = 'Reverse Repo/Standing Lending Facility - Rate'
            AND TRY_CAST(dv.PeriodID AS DATE) <= @WeekAgoDate
        ORDER BY TRY_CAST(dv.PeriodID AS DATE) DESC
    ) weekAgo ON 1=1
    LEFT JOIN (
        SELECT TOP 1 dv.Value
        FROM DataValue dv
        INNER JOIN DataCode dc ON dc.DataCodeID = dv.DataCodeID
        WHERE dc.[Item Name] = 'Reverse Repo/Standing Lending Facility - Rate'
            AND TRY_CAST(dv.PeriodID AS DATE) <= @YearAgoDate
        ORDER BY TRY_CAST(dv.PeriodID AS DATE) DESC
    ) yearAgo ON 1=1

    UNION ALL

    -- Average Weighted Call Money Rate (AWCMR)
    SELECT
        'Average Weighted Call Money Rate' AS ItemName,
        ISNULL(TRY_CONVERT(DECIMAL(18,2), yearAgo.Value), 0) AS YearAgoValue,
        ISNULL(TRY_CONVERT(DECIMAL(18,2), weekAgo.Value), 0) AS WeekAgoValue,
        ISNULL(TRY_CONVERT(DECIMAL(18,2), thisWeek.Value), 0) AS ThisWeek
    FROM (SELECT 1 AS DummyKey) dummy
    LEFT JOIN (
        SELECT dv.Value
        FROM DataValue dv
        INNER JOIN DataCode dc ON dc.DataCodeID = dv.DataCodeID
        WHERE dc.[Item Name] = 'Average Weighted Call Money Rate'
            AND dv.PeriodID = @CurrentPeriod
    ) thisWeek ON 1=1
    LEFT JOIN (
        SELECT TOP 1 dv.Value
        FROM DataValue dv
        INNER JOIN DataCode dc ON dc.DataCodeID = dv.DataCodeID
        WHERE dc.[Item Name] = 'Average Weighted Call Money Rate'
            AND TRY_CAST(dv.PeriodID AS DATE) <= @WeekAgoDate
        ORDER BY TRY_CAST(dv.PeriodID AS DATE) DESC
    ) weekAgo ON 1=1
    LEFT JOIN (
        SELECT TOP 1 dv.Value
        FROM DataValue dv
        INNER JOIN DataCode dc ON dc.DataCodeID = dv.DataCodeID
        WHERE dc.[Item Name] = 'Average Weighted Call Money Rate'
            AND TRY_CAST(dv.PeriodID AS DATE) <= @YearAgoDate
        ORDER BY TRY_CAST(dv.PeriodID AS DATE) DESC
    ) yearAgo ON 1=1

    UNION ALL

    -- Treasury Bill 91 Days
    SELECT
        'Treasury Bill Primary Market Auction Weighted Average Yield Rate -91 days' AS ItemName,
        ISNULL(TRY_CONVERT(DECIMAL(18,2), yearAgo.Value), 0) AS YearAgoValue,
        ISNULL(TRY_CONVERT(DECIMAL(18,2), weekAgo.Value), 0) AS WeekAgoValue,
        ISNULL(TRY_CONVERT(DECIMAL(18,2), thisWeek.Value), 0) AS ThisWeek
    FROM (SELECT 1 AS DummyKey) dummy
    LEFT JOIN (
        SELECT dv.Value
        FROM DataValue dv
        INNER JOIN DataCode dc ON dc.DataCodeID = dv.DataCodeID
        WHERE dc.[Item Name] = 'Treasury Bill Primary Market Auction Weighted Average Yield Rate -91 days'
            AND dv.PeriodID = @CurrentPeriod
    ) thisWeek ON 1=1
    LEFT JOIN (
        SELECT TOP 1 dv.Value
        FROM DataValue dv
        INNER JOIN DataCode dc ON dc.DataCodeID = dv.DataCodeID
        WHERE dc.[Item Name] = 'Treasury Bill Primary Market Auction Weighted Average Yield Rate -91 days'
            AND TRY_CAST(dv.PeriodID AS DATE) <= @WeekAgoDate
        ORDER BY TRY_CAST(dv.PeriodID AS DATE) DESC
    ) weekAgo ON 1=1
    LEFT JOIN (
        SELECT TOP 1 dv.Value
        FROM DataValue dv
        INNER JOIN DataCode dc ON dc.DataCodeID = dv.DataCodeID
        WHERE dc.[Item Name] = 'Treasury Bill Primary Market Auction Weighted Average Yield Rate -91 days'
            AND TRY_CAST(dv.PeriodID AS DATE) <= @YearAgoDate
        ORDER BY TRY_CAST(dv.PeriodID AS DATE) DESC
    ) yearAgo ON 1=1

    UNION ALL

    -- Treasury Bill 182 Days
    SELECT
        'Treasury Bill Primary Market Auction Weighted Average Yield Rate -182 days' AS ItemName,
        ISNULL(TRY_CONVERT(DECIMAL(18,2), yearAgo.Value), 0) AS YearAgoValue,
        ISNULL(TRY_CONVERT(DECIMAL(18,2), weekAgo.Value), 0) AS WeekAgoValue,
        ISNULL(TRY_CONVERT(DECIMAL(18,2), thisWeek.Value), 0) AS ThisWeek
    FROM (SELECT 1 AS DummyKey) dummy
    LEFT JOIN (
        SELECT dv.Value
        FROM DataValue dv
        INNER JOIN DataCode dc ON dc.DataCodeID = dv.DataCodeID
        WHERE dc.[Item Name] = 'Treasury Bill Primary Market Auction Weighted Average Yield Rate -182 days'
            AND dv.PeriodID = @CurrentPeriod
    ) thisWeek ON 1=1
    LEFT JOIN (
        SELECT TOP 1 dv.Value
        FROM DataValue dv
        INNER JOIN DataCode dc ON dc.DataCodeID = dv.DataCodeID
        WHERE dc.[Item Name] = 'Treasury Bill Primary Market Auction Weighted Average Yield Rate -182 days'
            AND TRY_CAST(dv.PeriodID AS DATE) <= @WeekAgoDate
        ORDER BY TRY_CAST(dv.PeriodID AS DATE) DESC
    ) weekAgo ON 1=1
    LEFT JOIN (
        SELECT TOP 1 dv.Value
        FROM DataValue dv
        INNER JOIN DataCode dc ON dc.DataCodeID = dv.DataCodeID
        WHERE dc.[Item Name] = 'Treasury Bill Primary Market Auction Weighted Average Yield Rate -182 days'
            AND TRY_CAST(dv.PeriodID AS DATE) <= @YearAgoDate
        ORDER BY TRY_CAST(dv.PeriodID AS DATE) DESC
    ) yearAgo ON 1=1

    UNION ALL

    -- Treasury Bill 364 Days
    SELECT
        'Treasury Bill Primary Market Auction Weighted Average Yield Rate -364 days' AS ItemName,
        ISNULL(TRY_CONVERT(DECIMAL(18,2), yearAgo.Value), 0) AS YearAgoValue,
        ISNULL(TRY_CONVERT(DECIMAL(18,2), weekAgo.Value), 0) AS WeekAgoValue,
        ISNULL(TRY_CONVERT(DECIMAL(18,2), thisWeek.Value), 0) AS ThisWeek
    FROM (SELECT 1 AS DummyKey) dummy
    LEFT JOIN (
        SELECT dv.Value
        FROM DataValue dv
        INNER JOIN DataCode dc ON dc.DataCodeID = dv.DataCodeID
        WHERE dc.[Item Name] = 'Treasury Bill Primary Market Auction Weighted Average Yield Rate -364 days'
            AND dv.PeriodID = @CurrentPeriod
    ) thisWeek ON 1=1
    LEFT JOIN (
        SELECT TOP 1 dv.Value
        FROM DataValue dv
        INNER JOIN DataCode dc ON dc.DataCodeID = dv.DataCodeID
        WHERE dc.[Item Name] = 'Treasury Bill Primary Market Auction Weighted Average Yield Rate -364 days'
            AND TRY_CAST(dv.PeriodID AS DATE) <= @WeekAgoDate
        ORDER BY TRY_CAST(dv.PeriodID AS DATE) DESC
    ) weekAgo ON 1=1
    LEFT JOIN (
        SELECT TOP 1 dv.Value
        FROM DataValue dv
        INNER JOIN DataCode dc ON dc.DataCodeID = dv.DataCodeID
        WHERE dc.[Item Name] = 'Treasury Bill Primary Market Auction Weighted Average Yield Rate -364 days'
            AND TRY_CAST(dv.PeriodID AS DATE) <= @YearAgoDate
        ORDER BY TRY_CAST(dv.PeriodID AS DATE) DESC
    ) yearAgo ON 1=1

    UNION ALL

    -- Average Weighted Prime Lending Rate (AWPR)
    SELECT
        'Average Weighted Prime Lending Rate (AWPR) - Weekly' AS ItemName,
        ISNULL(TRY_CONVERT(DECIMAL(18,2), yearAgo.Value), 0) AS YearAgoValue,
        ISNULL(TRY_CONVERT(DECIMAL(18,2), weekAgo.Value), 0) AS WeekAgoValue,
        ISNULL(TRY_CONVERT(DECIMAL(18,2), thisWeek.Value), 0) AS ThisWeek
    FROM (SELECT 1 AS DummyKey) dummy
    LEFT JOIN (
        SELECT dv.Value
        FROM DataValue dv
        INNER JOIN DataCode dc ON dc.DataCodeID = dv.DataCodeID
        WHERE dc.[Item Name] = 'Average Weighted Prime Lending Rate (AWPR) - Weekly'
            AND dv.PeriodID = @CurrentPeriod
    ) thisWeek ON 1=1
    LEFT JOIN (
        SELECT TOP 1 dv.Value
        FROM DataValue dv
        INNER JOIN DataCode dc ON dc.DataCodeID = dv.DataCodeID
        WHERE dc.[Item Name] = 'Average Weighted Prime Lending Rate (AWPR) - Weekly'
            AND TRY_CAST(dv.PeriodID AS DATE) <= @WeekAgoDate
        ORDER BY TRY_CAST(dv.PeriodID AS DATE) DESC
    ) weekAgo ON 1=1
    LEFT JOIN (
        SELECT TOP 1 dv.Value
        FROM DataValue dv
        INNER JOIN DataCode dc ON dc.DataCodeID = dv.DataCodeID
        WHERE dc.[Item Name] = 'Average Weighted Prime Lending Rate (AWPR) - Weekly'
            AND TRY_CAST(dv.PeriodID AS DATE) <= @YearAgoDate
        ORDER BY TRY_CAST(dv.PeriodID AS DATE) DESC
    ) yearAgo ON 1=1;
END;
GO;




-----------------------------------------------------

-- =====================================================
-- Performance Indexes for SP_GetPrices_ByPeriod
-- =====================================================

-- Index on DataValue table to optimize period-based queries
-- This significantly improves JOIN performance when filtering by multiple periods
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DataValue_PeriodID_DataCodeID' AND object_id = OBJECT_ID('DataValue'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_DataValue_PeriodID_DataCodeID 
    ON DataValue (PeriodID, DataCodeID) 
    INCLUDE (Value);
    PRINT 'Created index: IX_DataValue_PeriodID_DataCodeID on DataValue table';
END
ELSE
BEGIN
    PRINT 'Index IX_DataValue_PeriodID_DataCodeID already exists on DataValue table';
END
GO

-- Index on DataCode table to optimize frequency and item name filtering
-- This improves WHERE clause performance for FrequencyCode and Item Name lookups
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DataCode_FrequencyCode_ItemName' AND object_id = OBJECT_ID('DataCode'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_DataCode_FrequencyCode_ItemName 
    ON DataCode (FrequencyCode) 
    INCLUDE ([Item Name], [Short Name], Unit, [Scale]);
    PRINT 'Created index: IX_DataCode_FrequencyCode_ItemName on DataCode table';
END
ELSE
BEGIN
    PRINT 'Index IX_DataCode_FrequencyCode_ItemName already exists on DataCode table';
END
GO

-- Additional index for DataValue table to optimize date range queries
-- This helps with ORDER BY PeriodID operations and date filtering
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DataValue_PeriodID_Value' AND object_id = OBJECT_ID('DataValue'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_DataValue_PeriodID_Value 
    ON DataValue (PeriodID DESC) 
    INCLUDE (DataCodeID, Value);
    PRINT 'Created index: IX_DataValue_PeriodID_Value on DataValue table';
END
ELSE
BEGIN
    PRINT 'Index IX_DataValue_PeriodID_Value already exists on DataValue table';
END
GO

-- Index to optimize DataCode lookups by Item Name (for market filtering)
-- This improves LIKE operations on Item Name column
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DataCode_ItemName_Frequency' AND object_id = OBJECT_ID('DataCode'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_DataCode_ItemName_Frequency 
    ON DataCode ([Item Name], FrequencyCode) 
    INCLUDE (DataCodeID, [Short Name]);
    PRINT 'Created index: IX_DataCode_ItemName_Frequency on DataCode table';
END
ELSE
BEGIN
    PRINT 'Index IX_DataCode_ItemName_Frequency already exists on DataCode table';
END
GO