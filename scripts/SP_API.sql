-- =========================================================
-- Stored Procedure for Price Indices Data (NCPI & CCPI) 1.1
-- =========================================================
CREATE OR ALTER PROCEDURE SP_GetPriceIndices_ByPeriod
    @CurrentPeriod NVARCHAR(20) = NULL, -- e.g. '2025-12-12' in yyyy-MM-dd format; NULL = latest
    @Type NVARCHAR(10) = NULL          -- 'NCPI', 'CCPI', or NULL for both
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @year INT;
    DECLARE @month INT;
    DECLARE @curDate DATE;
    DECLARE @dbPeriod NVARCHAR(20);

    SET @Type = UPPER(LTRIM(RTRIM(@Type)));

    DECLARE @TargetItems TABLE (ItemName NVARCHAR(200));

    IF @Type IS NULL OR @Type = 'NCPI'
    BEGIN
        INSERT INTO @TargetItems (ItemName) VALUES
            ('NCPI(2021=100)(Headline)'),
            ('NCPI(2021=100)(Headline)(Monthly %)'),
            ('NCPI(2021=100)(Headline)(Annual Average %)'),
            ('NCPI(2021=100)(Headline)(Year-on-Year %)'),
            ('NCPI(2021=100)(CORE)'),
            ('NCPI(2021=100)(CORE)(Annual Average %)'),
            ('NCPI(2021=100)(CORE)(Year-on-Year %)');
    END

    IF @Type IS NULL OR @Type = 'CCPI'
    BEGIN
        INSERT INTO @TargetItems (ItemName) VALUES
            ('CCPI(2021=100)(Headline)'),
            ('CCPI(2021=100)(Headline)(Monthly %)'),
            ('CCPI(2021=100)(Headline)(Annual Average %)'),
            ('CCPI(2021=100)(Headline)(Year-on-Year %)'),
            ('CCPI(2021=100)(CORE)'),
            ('CCPI(2021=100)(CORE)(Annual Average %)'),
            ('CCPI(2021=100)(CORE)(Year-on-Year %)');
    END

    IF NOT EXISTS (SELECT 1 FROM @TargetItems)
    BEGIN
        RAISERROR('Invalid type. Expected: NCPI, CCPI, or NULL for both.', 16, 1);
        RETURN;
    END

    IF @CurrentPeriod IS NOT NULL
    BEGIN
        BEGIN TRY
            SET @year = TRY_CAST(SUBSTRING(@CurrentPeriod, 1, 4) AS INT);
            SET @month = TRY_CAST(SUBSTRING(@CurrentPeriod, 6, 2) AS INT);

            IF @year IS NULL OR @month IS NULL OR @month < 1 OR @month > 12
            BEGIN
                RAISERROR('Invalid period format. Expected: yyyy-MM-dd (e.g., 2025-12-12)', 16, 1);
                RETURN;
            END

            SET @curDate = DATEFROMPARTS(@year, @month, 1);
            SET @dbPeriod = FORMAT(@curDate, 'yyyy-MM') + '-00';
        END TRY
        BEGIN CATCH
            RAISERROR('Invalid period format. Expected: yyyy-MM-dd (e.g., 2025-12-12)', 16, 1);
            RETURN;
        END CATCH
    END
    ELSE
    BEGIN
        SELECT TOP (1)
            @dbPeriod = dv.PeriodID
        FROM dbo.DataCode dc
        INNER JOIN dbo.DataValue dv ON dv.DataCodeID = dc.DataCodeID
        INNER JOIN @TargetItems ti ON ti.ItemName = dc.[Item Name]
        WHERE dv.FrequencyCode = 'M'
          AND dv.PeriodID LIKE '____-__-__'
        ORDER BY dv.PeriodID DESC;

        IF @dbPeriod IS NULL
        BEGIN
            RAISERROR('No data available for the requested type.', 16, 1);
            RETURN;
        END
    END

    SELECT
        dc.[Item Name] AS ItemName,
        dc.DataCodeID,
        @dbPeriod AS PeriodID,
        MAX(CASE WHEN dv.PeriodID = @dbPeriod THEN TRY_CONVERT(DECIMAL(18,1), dv.Value) END) AS CurrentValue
    FROM dbo.DataCode dc
    INNER JOIN dbo.DataValue dv ON dv.DataCodeID = dc.DataCodeID
    INNER JOIN @TargetItems ti ON ti.ItemName = dc.[Item Name]
    WHERE dv.FrequencyCode = 'M'
      AND dv.PeriodID = @dbPeriod
    GROUP BY dc.DataCodeID, dc.[Item Name]
    ORDER BY dc.[Item Name];
END;
GO;

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
			SET @CurrentPeriod =  CAST(@year AS NVARCHAR(4)) + '-' + RIGHT('0' + CAST(@month AS NVARCHAR(2)), 2) + '-00';
		END

		IF @FrequencyCode IS NOT NULL AND @FrequencyCode = 'D'
		BEGIN
			SET @CurrentPeriod =  CAST(@year AS NVARCHAR(4)) + '-' + RIGHT('0' + CAST(@month AS NVARCHAR(2)), 2) + '-' + RIGHT('0' + CAST(@date AS NVARCHAR(2)), 2);
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
			SET @CurrentPeriod =  CAST(@year AS NVARCHAR(4)) + '-' + RIGHT('0' + CAST(@month AS NVARCHAR(2)), 2) + '-00';
		END

		IF @FrequencyCode IS NOT NULL AND @FrequencyCode = 'D'
		BEGIN
			SET @CurrentPeriod =  CAST(@year AS NVARCHAR(4)) + '-' + RIGHT('0' + CAST(@month AS NVARCHAR(2)), 2) + '-' + RIGHT('0' + CAST(@date AS NVARCHAR(2)), 2);
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
--                LTRIM(RTRIM(REVERSE(SUBSTRING(REVERSE(dc.[Item Name]), 1, CHARINDEX('-', REVERSE(dc.[Item Name])) - 1)))) AS 'Item',
                dc.[Item Name] AS 'Item',
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
			SET @CurrentPeriod =  CAST(@year AS NVARCHAR(4)) + '-' + RIGHT('0' + CAST(@month AS NVARCHAR(2)), 2) + '-00';
		END

		IF @FrequencyCode IS NOT NULL AND @FrequencyCode = 'D'
		BEGIN
			SET @CurrentPeriod =  CAST(@year AS NVARCHAR(4)) + '-' + RIGHT('0' + CAST(@month AS NVARCHAR(2)), 2) + '-' + RIGHT('0' + CAST(@date AS NVARCHAR(2)), 2);
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

--================================================================
-- Stored Procedure for Employment Data 1.7
--================================================================
CREATE OR ALTER PROCEDURE SP_GetEmployment_ByPeriod
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
			SET @CurrentPeriod =  CAST(@year AS NVARCHAR(4)) + '-' + RIGHT('0' + CAST(@month AS NVARCHAR(2)), 2) + '-00';
		END

		IF @FrequencyCode IS NOT NULL AND @FrequencyCode = 'D'
		BEGIN
			SET @CurrentPeriod =  CAST(@year AS NVARCHAR(4)) + '-' + RIGHT('0' + CAST(@month AS NVARCHAR(2)), 2) + '-' + RIGHT('0' + CAST(@date AS NVARCHAR(2)), 2);
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
				('Labour Statistics-Labour Force Participation Rate-Sri Lanka'),
				('Labour Statistics-Unemployment Rate-Sri Lanka'),
				('Labour Statistics-Employment-Industrial Category-Agriculture (as a % of Employment)'),
				('Labour Statistics-Employment-Industrial Category-Industry (as a % of Employment)'),
				('Labour Statistics-Employment-Industrial Category-Services (as a % of Employment)');

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

--================================================================
-- Stored Procedure for Wage Rate Indices Data 1.8
--================================================================
CREATE OR ALTER PROCEDURE SP_GetWageRateIndices_ByPeriod
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
			SET @CurrentPeriod =  CAST(@year AS NVARCHAR(4)) + '-' + RIGHT('0' + CAST(@month AS NVARCHAR(2)), 2) + '-00';
		END

		IF @FrequencyCode IS NOT NULL AND @FrequencyCode = 'D'
		BEGIN
			SET @CurrentPeriod =  CAST(@year AS NVARCHAR(4)) + '-' + RIGHT('0' + CAST(@month AS NVARCHAR(2)), 2) + '-' + RIGHT('0' + CAST(@date AS NVARCHAR(2)), 2);
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
				('Public Sector Wage Rate Index (2016=100)(Nominal)'),
				('Informal Private Sector Wage Rate Index (2018=100)(Nominal)'),
				('Informal Private Sector Wage Rate Index (2018=100)(Nominal) - Agriculture'),
				('Informal Private Sector Wage Rate Index (2018=100)(Nominal) - Industry'),
				('Informal Private Sector Wage Rate Index (2018=100)(Nominal) - Services'),
				('Public Sector Wage Rate Index (2016=100)(Nominal) - Y-o-Y Change'),
				('Informal Private Sector Wage Rate Index (2018=100)(Nominal) - Y-o-Y Change'),
				('Informal Private Sector Wage Rate Index (2018=100)(Nominal) - Agriculture - Y-o-Y Change'),
				('Informal Private Sector Wage Rate Index (2018=100)(Nominal) - Industry - Y-o-Y Change'),
				('Informal Private Sector Wage Rate Index (2018=100)(Nominal) - Services - Y-o-Y Change');

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

--================================================================
-- Stored Procedure for Wage Rate Indices Data 1.9
--================================================================
CREATE OR ALTER PROCEDURE SP_GetWageRateIndices_ByPeriod
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
			SET @CurrentPeriod =  CAST(@year AS NVARCHAR(4)) + '-' + RIGHT('0' + CAST(@month AS NVARCHAR(2)), 2) + '-00';
		END

		IF @FrequencyCode IS NOT NULL AND @FrequencyCode = 'D'
		BEGIN
			SET @CurrentPeriod =  CAST(@year AS NVARCHAR(4)) + '-' + RIGHT('0' + CAST(@month AS NVARCHAR(2)), 2) + '-' + RIGHT('0' + CAST(@date AS NVARCHAR(2)), 2);
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
				('Crude Oil Futures Prices - Brent (Benchmark price)'),
				('Crude Oil Futures Prices - WTI (Benchmark price)'),
				('CPC Import Prices');

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

--================================================================
-- Stored Procedure for Daily Electricity Generation Data 1.0
--================================================================
CREATE OR ALTER PROCEDURE SP_GetDailyElectricityGeneration
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
			SET @CurrentPeriod =  CAST(@year AS NVARCHAR(4)) + '-' + RIGHT('0' + CAST(@month AS NVARCHAR(2)), 2) + '-00';
		END

		IF @FrequencyCode IS NOT NULL AND @FrequencyCode = 'D'
		BEGIN
			SET @CurrentPeriod =  CAST(@year AS NVARCHAR(4)) + '-' + RIGHT('0' + CAST(@month AS NVARCHAR(2)), 2) + '-' + RIGHT('0' + CAST(@date AS NVARCHAR(2)), 2);
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
				('Electricity Generation - Peak Demand'),
				('Electricity Generation - Total Energy'),
				('Electricity Generation - Hydro'),
				('Electricity Generation - Thermal Coal'),
				('Electricity Generation - Thermal Oil'),
				('Electricity Generation - Wind'),
				('Electricity Generation - Solar'),
				('Electricity Generation - Biomass');

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

--================================================================
-- Stored Procedure for Policy Interest Rate Data 2.1
--================================================================
CREATE OR ALTER PROCEDURE SP_GetPolicyInterestRate_ByPeriod
    @CurrentPeriod NVARCHAR(20) = NULL,
    @FrequencyCode NVARCHAR(10) = NULL,
    @YearCode NVARCHAR(20) = NULL,
    @QuarterCode INT = NULL,
	@TypeCode NVARCHAR(20) = NULL
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
			SET @CurrentPeriod =  CAST(@year AS NVARCHAR(4)) + '-' + RIGHT('0' + CAST(@month AS NVARCHAR(2)), 2) + '-00';
		END

		IF @FrequencyCode IS NOT NULL AND @FrequencyCode = 'D'
		BEGIN
			SET @CurrentPeriod =  CAST(@year AS NVARCHAR(4)) + '-' + RIGHT('0' + CAST(@month AS NVARCHAR(2)), 2) + '-' + RIGHT('0' + CAST(@date AS NVARCHAR(2)), 2);
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
		IF @TypeCode IS NOT NULL AND @TypeCode = 'PolicyInterestRate'
		BEGIN
			INSERT INTO @TargetItems (ItemName) VALUES
			('Policy Rates - Overnight Policy Rate (OPR)')
		END
		ELSE IF @TypeCode IS NOT NULL AND @TypeCode = 'StandingFacilityRates'
		BEGIN
				INSERT INTO @TargetItems (ItemName) VALUES
				('Repo/Standing Deposit Facility - Rate'),
				('Reverse Repo/Standing Lending Facility - Rate')
		END
		ELSE IF @TypeCode IS NOT NULL AND @TypeCode = 'CallMoneyMarket'
		BEGIN
				INSERT INTO @TargetItems (ItemName) VALUES
				('Average Weighted Call Money Rate')
		END
		ELSE IF @TypeCode IS NOT NULL AND @TypeCode = 'TreasuryBillYield'
		BEGIN
				INSERT INTO @TargetItems (ItemName) VALUES
				('Treasury Bill Primary Market Auction Weighted Average Yield Rate -91 days'),
				('Treasury Bill Primary Market Auction Weighted Average Yield Rate -182 days'),
				('Treasury Bill Primary Market Auction Weighted Average Yield Rate -364 days')
		END
		ELSE IF @TypeCode IS NOT NULL AND @TypeCode = 'LendingDepositRates'
		BEGIN
				INSERT INTO @TargetItems (ItemName) VALUES
				('Average Weighted Prime Lending Rate (AWPR) - Weekly'),
				('Licensed Commercial Banks - Savings Deposits Rates_Min'),
				('Licensed Commercial Banks - Savings Deposits Rates_Max'),
				('Licensed Commercial Banks - One Year Fixed Deposit Rates_Min'),
				('Licensed Commercial Banks - One Year Fixed Deposit Rates_Max'),
				('Average Weighted Deposit Rate'),
				('Average Weighted Fixed Deposit Rate'),
				('Average Weighted New Deposit Rate'),
				('Average Weighted New Fixed Deposit Rate'),
				('Average Weighted Lending Rate'),
				('Average Weighted New Lending Rate'),
				('Average Weighted SME Rate (AWSR)'),
				('Average Weighted New SME Rate (AWNSR)')
		END
		ELSE IF @TypeCode IS NOT NULL AND @TypeCode = 'NSB'
		BEGIN
				INSERT INTO @TargetItems (ItemName) VALUES
				('National Savings Bank - Savings Deposits Rates'),
				('National Savings Bank - One Year Fixed Deposit Rates')
		END
		ELSE IF @TypeCode IS NOT NULL AND @TypeCode = 'BankwiseAWPR'
		BEGIN
				INSERT INTO @TargetItems (ItemName) VALUES
				('Bankwise- AWPR (Bank of Ceylon)'),
				('Bankwise- AWPR (People''s Bank)'),
				('Bankwise- AWPR (Hatton National Bank)'),
				('Bankwise- AWPR (Commercial Bank of Ceylon)'),
				('Bankwise- AWPR (Sampath Bank)'),
				('Bankwise- AWPR (Seylan Bank)'),
				('Bankwise- AWPR (Union Bank of Colombo)'),
				('Bankwise- AWPR (Pan Asia Banking Corporation)'),
				('Bankwise- AWPR (Nations Trust Bank)'),
				('Bankwise- AWPR (DFCC Bank)'),
				('Bankwise- AWPR (NDB Bank )'),
				('Bankwise- AWPR (Amana Bank)'),
				('Bankwise- AWPR (Cargills Bank)'),
				('Bankwise- AWPR (HSBC)'),
				('Bankwise- AWPR (Standard Chartered Bank)'),
				('Bankwise- AWPR (Citi Bank)'),
				('Bankwise- AWPR (Deutsche Bank)'),
				('Bankwise- AWPR (Habib Bank)'),
				('Bankwise- AWPR (Indian Bank)'),
				('Bankwise- AWPR (Indian Overseas Bank)'),
				('Bankwise- AWPR (MCB Bank)'),
				('Bankwise- AWPR (State Bank of India)'),
				('Bankwise- AWPR (Public Bank)'),
				('Bankwise- AWPR (Bank of China)');
        END
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

--================================================================
-- Stored Procedure for Money Supply Data 2.2
--================================================================
CREATE OR ALTER PROCEDURE SP_GetMoneySupply_ByPeriod
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
			SET @CurrentPeriod =  CAST(@year AS NVARCHAR(4)) + '-' + RIGHT('0' + CAST(@month AS NVARCHAR(2)), 2) + '-00';
		END

		IF @FrequencyCode IS NOT NULL AND @FrequencyCode = 'D'
		BEGIN
			SET @CurrentPeriod =  CAST(@year AS NVARCHAR(4)) + '-' + RIGHT('0' + CAST(@month AS NVARCHAR(2)), 2) + '-' + RIGHT('0' + CAST(@date AS NVARCHAR(2)), 2);
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
				('Average Weighted Prime Lending Rate (AWPR) - Weekly'),
				('Licensed Commercial Banks - Savings Deposits Rates_Min'),
				('Licensed Commercial Banks - Savings Deposits Rates_Max'),
				('Licensed Commercial Banks - One Year Fixed Deposit Rates_Min'),
				('Licensed Commercial Banks - One Year Fixed Deposit Rates_Max'),
				('Average Weighted Deposit Rate'),
				('Average Weighted Fixed Deposit Rate'),
				('Average Weighted New Deposit Rate'),
				('Average Weighted New Fixed Deposit Rate'),
				('Average Weighted Lending Rate'),
				('Average Weighted New Lending Rate'),
				('Average Weighted SME Rate (AWSR)'),
				('Average Weighted New SME Rate (AWNSR)'),
				('National Savings Bank - Savings Deposits Rates'),
				('National Savings Bank - One Year Fixed Deposit Rates'),
				('Bankwise- AWPR (Bank of Ceylon)'),
				('Bankwise- AWPR (People''s Bank)'),
				('Bankwise- AWPR (Hatton National Bank)'),
				('Bankwise- AWPR (Commercial Bank of Ceylon)'),
				('Bankwise- AWPR (Sampath Bank)'),
				('Bankwise- AWPR (Seylan Bank)'),
				('Bankwise- AWPR (Union Bank of Colombo)');

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

--================================================================
-- Stored Procedure for Reserve Money Data 2.3
--================================================================
CREATE OR ALTER PROCEDURE SP_GetReserveMoney_ByPeriod
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
			SET @CurrentPeriod =  CAST(@year AS NVARCHAR(4)) + '-' + RIGHT('0' + CAST(@month AS NVARCHAR(2)), 2) + '-00';
		END

		IF @FrequencyCode IS NOT NULL AND @FrequencyCode = 'D'
		BEGIN
			SET @CurrentPeriod =  CAST(@year AS NVARCHAR(4)) + '-' + RIGHT('0' + CAST(@month AS NVARCHAR(2)), 2) + '-' + RIGHT('0' + CAST(@date AS NVARCHAR(2)), 2);
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
				('Reserve Money (Daily Estimate)'),
				('Currency in Circulation');

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

--================================================================
-- Stored Procedure for Money Market Activity Data 2.4
--================================================================
CREATE OR ALTER PROCEDURE SP_GetMoneyMarketActivity_ByPeriod
    @CurrentPeriod NVARCHAR(20) = NULL,
    @FrequencyCode NVARCHAR(10) = NULL,
    @YearCode NVARCHAR(20) = NULL,
    @QuarterCode INT = NULL,
	@TypeCode NVARCHAR(20) = NULL
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
			SET @CurrentPeriod =  CAST(@year AS NVARCHAR(4)) + '-' + RIGHT('0' + CAST(@month AS NVARCHAR(2)), 2) + '-00';
		END

		IF @FrequencyCode IS NOT NULL AND @FrequencyCode = 'D'
		BEGIN
			SET @CurrentPeriod =  CAST(@year AS NVARCHAR(4)) + '-' + RIGHT('0' + CAST(@month AS NVARCHAR(2)), 2) + '-' + RIGHT('0' + CAST(@date AS NVARCHAR(2)), 2);
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

		IF @TypeCode IS NOT NULL AND @TypeCode = 'MoneyMarketActivity'
		BEGIN
			INSERT INTO @TargetItems (ItemName) VALUES
					('Average Weighted Call Money Rate)'),
					('Inter Bank Call - Gross Volume');
		END
		ELSE IF @TypeCode IS NOT NULL AND @TypeCode = 'MoneyMarketActivity_Detailed'
		BEGIN
			INSERT INTO @TargetItems (ItemName) VALUES
					('Market Repo - Weighted Average Rate)'),
					('Market Repo - Gross Volume');
		END

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


--================================================================
-- Stored Procedure for CBSL Securities Portfolio Data 2.5
--================================================================
CREATE OR ALTER PROCEDURE SP_GetCbslSecuritiesPortfolio_ByPeriod
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
			SET @CurrentPeriod =  CAST(@year AS NVARCHAR(4)) + '-' + RIGHT('0' + CAST(@month AS NVARCHAR(2)), 2) + '-00';
		END

		IF @FrequencyCode IS NOT NULL AND @FrequencyCode = 'D'
		BEGIN
			SET @CurrentPeriod =  CAST(@year AS NVARCHAR(4)) + '-' + RIGHT('0' + CAST(@month AS NVARCHAR(2)), 2) + '-' + RIGHT('0' + CAST(@date AS NVARCHAR(2)), 2);
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
			('CBSL Holdings of Gov Securities (Face Value)'),
			('CBSL Holdings of Gov Securities (Book Value)');

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

--================================================================
-- Stored Procedure for Credit Cards Data 2.71
--================================================================
CREATE OR ALTER PROCEDURE SP_GetCreditCards_ByPeriod
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
			SET @CurrentPeriod =  CAST(@year AS NVARCHAR(4)) + '-' + RIGHT('0' + CAST(@month AS NVARCHAR(2)), 2) + '-00';
		END

		IF @FrequencyCode IS NOT NULL AND @FrequencyCode = 'D'
		BEGIN
			SET @CurrentPeriod =  CAST(@year AS NVARCHAR(4)) + '-' + RIGHT('0' + CAST(@month AS NVARCHAR(2)), 2) + '-' + RIGHT('0' + CAST(@date AS NVARCHAR(2)), 2);
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
			('Credit Card - Total Number of Active Cards'),
			('Credit Card - Local (accepted only locally)'),
			('Credit Card - Global (accepted globally)'),
			('Credit Card - Outstanding balance'),
			('Credit Card - Local Outstanding'),
			('Credit Card - Global Outstanding');

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

--================================================================
-- Stored Procedure for Commercial Paper Issue Data 2.72
--================================================================
CREATE OR ALTER PROCEDURE SP_GetCommercialPaperIssue_ByPeriod
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
			SET @CurrentPeriod =  CAST(@year AS NVARCHAR(4)) + '-' + RIGHT('0' + CAST(@month AS NVARCHAR(2)), 2) + '-00';
		END

		IF @FrequencyCode IS NOT NULL AND @FrequencyCode = 'D'
		BEGIN
			SET @CurrentPeriod =  CAST(@year AS NVARCHAR(4)) + '-' + RIGHT('0' + CAST(@month AS NVARCHAR(2)), 2) + '-' + RIGHT('0' + CAST(@date AS NVARCHAR(2)), 2);
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
			('Commercial Paper - Total Issues - Cumulative'),
			('Commercial Paper - Outstanding (as at end of the period )')

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

--================================================================
-- Stored Procedure for Commercial Paper Issue Data 2.8
--================================================================
CREATE OR ALTER PROCEDURE SP_GetShareMarket_ByPeriod
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
			SET @CurrentPeriod =  CAST(@year AS NVARCHAR(4)) + '-' + RIGHT('0' + CAST(@month AS NVARCHAR(2)), 2) + '-00';
		END

		IF @FrequencyCode IS NOT NULL AND @FrequencyCode = 'D'
		BEGIN
			SET @CurrentPeriod =  CAST(@year AS NVARCHAR(4)) + '-' + RIGHT('0' + CAST(@month AS NVARCHAR(2)), 2) + '-' + RIGHT('0' + CAST(@date AS NVARCHAR(2)), 2);
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
			('EQUITY- All share price index'),
			('EQUITY-S&P SL20 Index'),
			('EQUITY- Daily Turnover'),
			('EQUITY-Market Capitalization'),
			('EQUITY- Foreign Purchases'),
			('EQUITY-Foreign Sales'),
			('EQUITY- Net Foreign Purchases');

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

--================================================================
-- Stored Procedure for Government Finance Data 3.1
--================================================================
CREATE OR ALTER PROCEDURE SP_GetGovernmentFinance_ByPeriod
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
			SET @CurrentPeriod =  CAST(@year AS NVARCHAR(4)) + '-' + RIGHT('0' + CAST(@month AS NVARCHAR(2)), 2) + '-00';
		END

		IF @FrequencyCode IS NOT NULL AND @FrequencyCode = 'D'
		BEGIN
			SET @CurrentPeriod =  CAST(@year AS NVARCHAR(4)) + '-' + RIGHT('0' + CAST(@month AS NVARCHAR(2)), 2) + '-' + RIGHT('0' + CAST(@date AS NVARCHAR(2)), 2);
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
			('Revenue and Grants'),
			('Revenue'),
			('Tax Revenue'),
			('Non Tax Revenue'),
			('Grants'),
			('Expenditure and Net Lending'),
			('Recurrent Expenditure'),
			('Capital and Net Lending'),
			('Primary Balance'),
			('Budget Deficit');
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

--================================================================
-- Stored Procedure for Outstanding Central Government Debt Data 3.2
--================================================================
CREATE OR ALTER PROCEDURE SP_GetOutstandingCentralGovernmentDebt_ByPeriod
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
			SET @CurrentPeriod =  CAST(@year AS NVARCHAR(4)) + '-' + RIGHT('0' + CAST(@month AS NVARCHAR(2)), 2) + '-00';
		END

		IF @FrequencyCode IS NOT NULL AND @FrequencyCode = 'D'
		BEGIN
			SET @CurrentPeriod =  CAST(@year AS NVARCHAR(4)) + '-' + RIGHT('0' + CAST(@month AS NVARCHAR(2)), 2) + '-' + RIGHT('0' + CAST(@date AS NVARCHAR(2)), 2);
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
			('Total Outstanding Domestic Debt'),
			('Outstanding Treasury Bills'),
			('Outstanding Treasury Bonds'),
			('Outstanding Total Foreign Debt'),
			('Total outstanding central Government Debt');

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

--================================================================
-- Stored Procedure for Government Securities Data 3.3.1
--================================================================
CREATE OR ALTER PROCEDURE SP_GetGovernmentSecurities_ByPeriod
    @CurrentPeriod NVARCHAR(20) = NULL,
    @FrequencyCode NVARCHAR(10) = NULL,
    @YearCode NVARCHAR(20) = NULL,
    @QuarterCode INT = NULL,
	@TypeCode NVARCHAR(20) = NULL
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
			SET @CurrentPeriod =  CAST(@year AS NVARCHAR(4)) + '-' + RIGHT('0' + CAST(@month AS NVARCHAR(2)), 2) + '-00';
		END

		IF @FrequencyCode IS NOT NULL AND @FrequencyCode = 'D'
		BEGIN
			SET @CurrentPeriod =  CAST(@year AS NVARCHAR(4)) + '-' + RIGHT('0' + CAST(@month AS NVARCHAR(2)), 2) + '-' + RIGHT('0' + CAST(@date AS NVARCHAR(2)), 2);
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
		IF @TypeCode IS NOT NULL AND @TypeCode = 'primaryTreasuryBills'
		BEGIN
			INSERT INTO @TargetItems (ItemName) VALUES
				('Treasury Bill Primary Market Auction Weighted Average Yield Rate -91 days'),
				('Treasury Bill Primary Market Auction Weighted Average Yield Rate -182 days'),
				('Treasury Bill Primary Market Auction Weighted Average Yield Rate -364 days');
		END
		ELSE IF @TypeCode IS NOT NULL AND @TypeCode = 'primaryTreasuryBonds'
		BEGIN
			INSERT INTO @TargetItems (ItemName) VALUES
				('Outstanding Treasury Bonds'),
				('Treasury Bond Rate - 02 year'),
				('Treasury Bond Rate - 03 year'),
				('Treasury Bond Rate - 04 year'),
				('Treasury Bond Rate - 05 year'),
				('Treasury Bond Rate - 06 year'),
				('Treasury Bond Rate - 07 year'),
				('Treasury Bond Rate - 08 year'),
				('Treasury Bond Rate - 09 year'),
				('Treasury Bond Rate - 10 year'),
				('Treasury Bond Rate - 12 year'),
				('Treasury Bond Rate - 15 year'),
				('Treasury Bond Rate - 20 year'),
				('Treasury Bond Rate - 30 year');
		END
		ELSE IF @TypeCode IS NOT NULL AND @TypeCode = 'SecondaryMarketBuyingTreasuryBills'
		BEGIN
			INSERT INTO @TargetItems (ItemName) VALUES
				('Secondary Market Yield Rates - Treasury Bills - 91 days (Buying Rate)'),
				('Secondary Market Yield Rates - Treasury Bills - 182 days (Buying Rate)'),
				('Secondary Market Yield Rates - Treasury Bills - 364 days (Buying Rate)');
		END
		ELSE IF @TypeCode IS NOT NULL AND @TypeCode = 'SecondaryMarketBuyingTreasuryBonds'
		BEGIN
			INSERT INTO @TargetItems (ItemName) VALUES
				('Total Outstanding Domestic Debt'),
				('Secondary Market Yield Rates - Treasury Bonds - 2 years (Buying Rate)'),
				('Secondary Market Yield Rates - Treasury Bonds - 3 years (Buying Rate)'),
				('Secondary Market Yield Rates - Treasury Bonds - 4 years (Buying Rate)'),
				('Secondary Market Yield Rates - Treasury Bonds - 5 years (Buying Rate)'),
				('Secondary Market Yield Rates - Treasury Bonds - 6 years (Buying Rate)'),
				('Secondary Market Yield Rates - Treasury Bonds - 7 years (Buying Rate)'),
				('Secondary Market Yield Rates - Treasury Bonds - 8 years (Buying Rate)'),
				('Secondary Market Yield Rates - Treasury Bonds - 9 years (Buying Rate)'),
				('Secondary Market Yield Rates - Treasury Bonds - 10 years (Buying Rate)'),
				('Secondary Market Yield Rates - Treasury Bonds - 12 years (Buying Rate)'),
				('Secondary Market Yield Rates - Treasury Bonds - 15 years (Buying Rate)'),
				('Secondary Market Yield Rates - Treasury Bonds - 20 years (Buying Rate)'),
				('Secondary Market Yield Rates - Treasury Bonds - 30 years (Buying Rate)');
		END
		ELSE IF @TypeCode IS NOT NULL AND @TypeCode = 'SecondaryMarketSellingTreasuryBills'
		BEGIN
			INSERT INTO @TargetItems (ItemName) VALUES
				('Secondary Market Yield Rates - Treasury Bills - 91 days (Selling Rate)'),
				('Secondary Market Yield Rates - Treasury Bills - 182 days (Selling Rate)'),
				('Secondary Market Yield Rates - Treasury Bills - 364 days (Selling Rate)');
		END
		ELSE IF @TypeCode IS NOT NULL AND @TypeCode = 'SecondaryMarketSellingTreasuryBonds'
		BEGIN
			INSERT INTO @TargetItems (ItemName) VALUES
				('Secondary Market Yield Rates - Treasury Bonds - 2 years (Selling Rate)'),
				('Secondary Market Yield Rates - Treasury Bonds - 3 years (Selling Rate)'),
				('Secondary Market Yield Rates - Treasury Bonds - 4 years (Selling Rate'),
				('Secondary Market Yield Rates - Treasury Bonds - 5 years (Selling Rate)'),
				('Secondary Market Yield Rates - Treasury Bonds - 6 years (Selling Rate)'),
				('Secondary Market Yield Rates - Treasury Bonds - 7 years (Selling Rate)'),
				('Secondary Market Yield Rates - Treasury Bonds - 8 years (Selling Rate)'),
				('Secondary Market Yield Rates - Treasury Bonds - 9 years (Selling Rate)'),
				('Secondary Market Yield Rates - Treasury Bonds - 10 years (Selling Rate)'),
				('Secondary Market Yield Rates - Treasury Bonds - 12 years (Selling Rate)'),
				('Secondary Market Yield Rates - Treasury Bonds - 15 years (Selling Rate)'),
				('Secondary Market Yield Rates - Treasury Bonds - 20 years (Selling Rate)'),
				('Secondary Market Yield Rates - Treasury Bonds - 30 years (Selling Rate)');
		END
		ELSE IF @TypeCode IS NOT NULL AND @TypeCode = 'SecondaryMarketAverageTreasuryBills'
		BEGIN
			INSERT INTO @TargetItems (ItemName) VALUES
				('Secondary Market Yield Rates - Treasury Bills - 91 days (Average Rate)'),
				('Secondary Market Yield Rates - Treasury Bills - 182 days (Average Rate)'),
				('Secondary Market Yield Rates - Treasury Bills - 364 days (Average Rate)');
		END
		ELSE IF @TypeCode IS NOT NULL AND @TypeCode = 'SecondaryMarketAverageTreasuryBonds'
		BEGIN
			INSERT INTO @TargetItems (ItemName) VALUES
				('Secondary Market Yield Rates - Treasury Bonds - 2 years (Average Rate)'),
				('Secondary Market Yield Rates - Treasury Bonds - 3 years (Average Rate)'),
				('Secondary Market Yield Rates - Treasury Bonds - 4 years (Average Rate)'),
				('Secondary Market Yield Rates - Treasury Bonds - 5 years (Average Rate)'),
				('Secondary Market Yield Rates - Treasury Bonds - 6 years (Average Rate)'),
				('Secondary Market Yield Rates - Treasury Bonds - 7 years (Average Rate)'),
				('Secondary Market Yield Rates - Treasury Bonds - 8 years (Average Rate)'),
				('Secondary Market Yield Rates - Treasury Bonds - 9 years (Average Rate)'),
				('Secondary Market Yield Rates - Treasury Bonds - 10 years (Average Rate)'),
				('Secondary Market Yield Rates - Treasury Bonds - 12 years (Average Rate)'),
				('Secondary Market Yield Rates - Treasury Bonds - 15 years (Average Rate)'),
				('Secondary Market Yield Rates - Treasury Bonds - 20 years (Average Rate)'),
				('Secondary Market Yield Rates - Treasury Bonds - 30 years (Average Rate)');
		END

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

--================================================================
-- Stored Procedure for International Sovereign Bonds Data 3.3.2
--================================================================
CREATE OR ALTER PROCEDURE SP_GetInternationalSovereignBonds_ByPeriod
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
			SET @CurrentPeriod =  CAST(@year AS NVARCHAR(4)) + '-' + RIGHT('0' + CAST(@month AS NVARCHAR(2)), 2) + '-00';
		END

		IF @FrequencyCode IS NOT NULL AND @FrequencyCode = 'D'
		BEGIN
			SET @CurrentPeriod =  CAST(@year AS NVARCHAR(4)) + '-' + RIGHT('0' + CAST(@month AS NVARCHAR(2)), 2) + '-' + RIGHT('0' + CAST(@date AS NVARCHAR(2)), 2);
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
			('Secondary Market Yield Rates - ISBs (Maturity : 04/15/2028, Coupon Rate : 4%)'),
			('Secondary Market Yield Rates - ISBs (Maturity : 01/15/2030, Coupon Rate : 3.1%)'),
			('Secondary Market Yield Rates - ISBs (Maturity : 03/15/2033, Coupon Rate : 3.35%)'),
			('Secondary Market Yield Rates - ISBs (Maturity : 06/15/2035, Coupon Rate : 3.6%)'),
			('Secondary Market Yield Rates - ISBs (Maturity : 05/15/2036, Coupon Rate : 3.6%)'),
			('Secondary Market Yield Rates - ISBs (Maturity : 02/15/2038, Coupon Rate : 3.6%)'),
			('Secondary Market Yield Rates - ISBs (Maturity : 6/15/2038, Coupon Rate : 1%)');
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

--================================================================
-- Stored Procedure for Primary and Secondary Market Transactions Data 3.4
--================================================================
CREATE OR ALTER PROCEDURE SP_GetPrimarySecondaryMarketTransactions_ByPeriod
    @CurrentPeriod NVARCHAR(20) = NULL,
    @FrequencyCode NVARCHAR(10) = NULL,
    @YearCode NVARCHAR(20) = NULL,
    @QuarterCode INT = NULL,
	@TypeCode NVARCHAR(100) = NULL
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
			SET @CurrentPeriod =  CAST(@year AS NVARCHAR(4)) + '-' + RIGHT('0' + CAST(@month AS NVARCHAR(2)), 2) + '-00';
		END

		IF @FrequencyCode IS NOT NULL AND @FrequencyCode = 'D'
		BEGIN
			SET @CurrentPeriod =  CAST(@year AS NVARCHAR(4)) + '-' + RIGHT('0' + CAST(@month AS NVARCHAR(2)), 2) + '-' + RIGHT('0' + CAST(@date AS NVARCHAR(2)), 2);
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
		IF @TypeCode IS NOT NULL AND @TypeCode = 'outstandingStock'
		BEGIN
			INSERT INTO @TargetItems (ItemName) VALUES
				('Outstanding Stock of Government Securities - Treasury Bills'),
				('Outstanding Stock of Government Securities - Treasury Bonds'),
				('Outstanding Stock of Government Securities - T-bills and T-bonds held by Foreigners');
		END
		ELSE IF @TypeCode IS NOT NULL AND @TypeCode = 'primaryMarketActivityTreasuryBills'
		BEGIN
			INSERT INTO @TargetItems (ItemName) VALUES
				('Primary Market Activities of Treasury Bills -Amount Offered'),
				('Primary Market Activities of Treasury Bills -Total Bids Received'),
				('Primary Market Activities of Treasury Bills -Total Bids Accepted');
		END
		ELSE IF @TypeCode IS NOT NULL AND @TypeCode = 'primaryMarketActivityNonCompetitiveAllocation'
		BEGIN
			INSERT INTO @TargetItems (ItemName) VALUES
				('Treasury Bill Auction - Phase II, Non-competitive Allocation : Amount Raised');
		END
		ELSE IF @TypeCode IS NOT NULL AND @TypeCode = 'primaryMarketActivityTreasuryBonds'
		BEGIN
			INSERT INTO @TargetItems (ItemName) VALUES
				('Primary Market Activities of Treasury Bonds -Amount Offered'),
				('Primary Market Activities of Treasury Bonds -Total Bids Received'),
				('Primary Market Activities of Treasury Bonds -Total Bids Accepted');

		END
		ELSE IF @TypeCode IS NOT NULL AND @TypeCode = 'primaryMarketActivityDirectIssuanceWindow'
		BEGIN
			INSERT INTO @TargetItems (ItemName) VALUES
				('Treasury Bond Auction - Direct Issuance Window : Amount Raised');
		END
		ELSE IF @TypeCode IS NOT NULL AND @TypeCode = 'secondaryMarketActivityTreasuryBills'
		BEGIN
			INSERT INTO @TargetItems (ItemName) VALUES
				('Secondary Market Activities of Treasury Bills -Outright Transaction (Sales/Purchases)'),
				('Secondary Market Activities of Treasury Bills - Repo Transaction (Sales/Purchases)');
		END
		ELSE IF @TypeCode IS NOT NULL AND @TypeCode = 'secondaryMarketActivityTreasuryBonds'
		BEGIN
			INSERT INTO @TargetItems (ItemName) VALUES
				('Secondary Market Activities of Treasury Bonds -Outright Transaction (Sales/Purchases)'),
				('Secondary Market Activities of Treasury Bonds - Repo Transaction (Sales/Purchases)');
		END

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


--================================================================
-- Stored Procedure for Exchange Rates Data 4.1
--================================================================
CREATE OR ALTER PROCEDURE SP_GetExchangeRates_ByPeriod
    @CurrentPeriod NVARCHAR(20) = NULL,
    @FrequencyCode NVARCHAR(10) = NULL,
    @YearCode NVARCHAR(20) = NULL,
    @QuarterCode INT = NULL,
	@TypeCode NVARCHAR(100) = NULL
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
			SET @CurrentPeriod =  CAST(@year AS NVARCHAR(4)) + '-' + RIGHT('0' + CAST(@month AS NVARCHAR(2)), 2) + '-00';
		END

		IF @FrequencyCode IS NOT NULL AND @FrequencyCode = 'D'
		BEGIN
			SET @CurrentPeriod =  CAST(@year AS NVARCHAR(4)) + '-' + RIGHT('0' + CAST(@month AS NVARCHAR(2)), 2) + '-' + RIGHT('0' + CAST(@date AS NVARCHAR(2)), 2);
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
		IF @TypeCode IS NOT NULL AND @TypeCode = 'ExchangeRate_BuyingRate'
		BEGIN
			INSERT INTO @TargetItems (ItemName) VALUES
				('TT Rates -Buying USD'),
				('TT Rates -Buying GBP'),
				('TT Rates -Buying EURO');
		END
		ELSE IF @TypeCode IS NOT NULL AND @TypeCode = 'ExchangeRate_SellingRate'
		BEGIN
			INSERT INTO @TargetItems (ItemName) VALUES
				('TT Rates -Selling USD'),
				('TT Rates -Selling GBP'),
				('TT Rates -Selling EURO');
		END
		ELSE IF @TypeCode IS NOT NULL AND @TypeCode = 'ExchangeRate_AverageRate'
		BEGIN
			INSERT INTO @TargetItems (ItemName) VALUES
				('Exchange Rates - Average (USD)'),
				('Exchange Rates - Average (GBP)'),
				('Exchange Rates - Average (EURO)'),
				('Exchange Rates - Indicative (INR)'),
				('Special Drawing Rights - Indicative (SDR)');
		END
		ELSE IF @TypeCode IS NOT NULL AND @TypeCode = 'CentralBankPurchesesAndSales(USD mn)'
		BEGIN
			INSERT INTO @TargetItems (ItemName) VALUES
			--need to chage the item name in the data library ( this keys from Tablue server),values not loading
				('CentralBankIntervention Purchases'),
				('CentralBankIntervention Sales'),
				('Average Daily Interbank Volume');
		END
		ELSE IF @TypeCode IS NOT NULL AND @TypeCode = 'ForwardRates(Rs per USD)'
		BEGIN
			INSERT INTO @TargetItems (ItemName) VALUES
			--need to chage the item name in the data library ( this keys from Tablue server),values not loading
				('ForwardRates 1Mo'),
				('ForwardRates 3Mo'),
				('Average Daily Interbank Forward Volume'),
				('Outstand Forward Volumes');

		END
		
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

--================================================================
-- Stored Procedure for Tourism and Workers Remittance Data 4.2
--================================================================
CREATE OR ALTER PROCEDURE SP_GetTourismAndWorkersRemittance_ByPeriod
    @CurrentPeriod NVARCHAR(20) = NULL,
    @FrequencyCode NVARCHAR(10) = NULL,
    @YearCode NVARCHAR(20) = NULL,
    @QuarterCode INT = NULL,
	@TypeCode NVARCHAR(100) = NULL
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
			SET @CurrentPeriod =  CAST(@year AS NVARCHAR(4)) + '-' + RIGHT('0' + CAST(@month AS NVARCHAR(2)), 2) + '-00';
		END

		IF @FrequencyCode IS NOT NULL AND @FrequencyCode = 'D'
		BEGIN
			SET @CurrentPeriod =  CAST(@year AS NVARCHAR(4)) + '-' + RIGHT('0' + CAST(@month AS NVARCHAR(2)), 2) + '-' + RIGHT('0' + CAST(@date AS NVARCHAR(2)), 2);
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
		IF @TypeCode IS NOT NULL AND @TypeCode = 'TouristArrivals'
		BEGIN
			INSERT INTO @TargetItems (ItemName) VALUES
				('Total Tourist Arrivals'),
				('Tourists Arrivals-cumulative'),
				('Tourist Arrivals - Y-o-Y Change');
		END
		ELSE IF @TypeCode IS NOT NULL AND @TypeCode = 'EarningFromTourism'
		BEGIN
			INSERT INTO @TargetItems (ItemName) VALUES
				('Tourist Earnings'),
				('TEarnings from Tourism-cumulative-USD mn.'),
				('EarningsfromTourism YoYChange USD'), --Note: this item name is not in the data library, this key from tableau server
				('Tourist Earnings (In LKR)'),
				('Earnings from Tourism-cumulative-Rs. bn.'),
				('EarningsfromTourism YoYChange'); --Note: this item name is not in the data library, this key from tableau server
		END
		ELSE IF @TypeCode IS NOT NULL AND @TypeCode = 'WorkersRemittancesInflows'
		BEGIN
			INSERT INTO @TargetItems (ItemName) VALUES
				('Secondary income - Workers'' Remittances'),
				('Workers'' Remittances Inflows cumulative'),--Note: this item name is not in the data library, this key from tableau server
				('Workers'' Remittances Inflows Y-o-Y Change'),--Note: this item name is not in the data library, this key from tableau server
				('Workers'' Remittances Inflows'),--Note: this item name is not in the data library, this key from tableau server
				('Workers'' Remittances Inflows cumulative in LKR'),--Note: this item name is not in the data library, this key from tableau server
				('Workers'' Remittances Inflows Y-o-Y Change in LKR');--Note: this item name is not in the data library, this key from tableau server
		END
		
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

--4.3

--4.4

--================================================================
-- Stored Procedure for External Trade Data 4.5
--================================================================
CREATE OR ALTER PROCEDURE SP_GetExternalTrade_ByPeriod
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
			SET @CurrentPeriod =  CAST(@year AS NVARCHAR(4)) + '-' + RIGHT('0' + CAST(@month AS NVARCHAR(2)), 2) + '-00';
		END

		IF @FrequencyCode IS NOT NULL AND @FrequencyCode = 'D'
		BEGIN
			SET @CurrentPeriod =  CAST(@year AS NVARCHAR(4)) + '-' + RIGHT('0' + CAST(@month AS NVARCHAR(2)), 2) + '-' + RIGHT('0' + CAST(@date AS NVARCHAR(2)), 2);
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
				('Merchandise Exports - Total'),
				('Agricultural Exports'),
				('Industrial Exports - Total'),
				('Industrial Exports - Food, Beverages and Tobacco Exports'),
				('Industrial Exports - Textiles and Garments Exports'),
				('Industrial Exports - Petroleum Products Exports'),
				('Industrial Exports - Leather, Rubber Products Exports'),
				('Other Industrial Exports'),
				('Mineral Exports'),
				('Unclassified Exports'),
				('Merchandise Imports - Total'),
				('Consumer Goods Imports'),
				('Intermediate Goods Imports'),
				('Investment Goods Imports'),
				('Unclassified Imports'),
				('Trade Balance (in USD terms)'),
				('Merchandise Exports'),
				('Agricultural Exports'),
				('Industrial Exports - Total'),
				('Industrial Exports - Food, Beverages and Tobacco Exports'),
				('Industrial Exports - Textiles and Garments Exports'),
				('Industrial Exports - Petroleum Products Exports'),
				('Industrial Exports - Leather, Rubber Products Exports'),
				('Other Industrial Exports'),
				('Mineral Exports'),
				('Unclassified Exports'),
				('Merchandise Imports'),
				('Consumer Goods Imports'),
				('Intermediate Goods Imports'),
				('Investment Goods Imports'),
				('Unclassified Imports'),
				('Trade Balance (in Rs terms)');

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

--================================================================
-- Stored Procedure for Trade Indices Data 4.6
--================================================================
CREATE OR ALTER PROCEDURE SP_GetTradeIndices_ByPeriod
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
			SET @CurrentPeriod =  CAST(@year AS NVARCHAR(4)) + '-' + RIGHT('0' + CAST(@month AS NVARCHAR(2)), 2) + '-00';
		END

		IF @FrequencyCode IS NOT NULL AND @FrequencyCode = 'D'
		BEGIN
			SET @CurrentPeriod =  CAST(@year AS NVARCHAR(4)) + '-' + RIGHT('0' + CAST(@month AS NVARCHAR(2)), 2) + '-' + RIGHT('0' + CAST(@date AS NVARCHAR(2)), 2);
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
				('Merchandise Export Value Index (2010=100)'),
				('Merchandise Export Volume Index (2010=100)'),
				('Merchandise Export Unit Value Index (2010=100)'),
				('Merchandise Import Value Index (2010=100)'),
				('Merchandise Import Volume Index (2010=100)'),
				('Merchandise Import Unit Value Index (2010=100)'),
				('Terms of Trade (2010=100)');

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

--================================================================
-- Stored Procedure for Commodity Price Data 4.7
--================================================================
CREATE OR ALTER PROCEDURE SP_GetCommodityPrice_ByPeriod
    @CurrentPeriod NVARCHAR(20) = NULL,
    @FrequencyCode NVARCHAR(10) = NULL,
    @YearCode NVARCHAR(20) = NULL,
    @QuarterCode INT = NULL,
	@TypeCode NVARCHAR(50) = NULL
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
			SET @CurrentPeriod =  CAST(@year AS NVARCHAR(4)) + '-' + RIGHT('0' + CAST(@month AS NVARCHAR(2)), 2) + '-00';
		END

		IF @FrequencyCode IS NOT NULL AND @FrequencyCode = 'D'
		BEGIN
			SET @CurrentPeriod =  CAST(@year AS NVARCHAR(4)) + '-' + RIGHT('0' + CAST(@month AS NVARCHAR(2)), 2) + '-' + RIGHT('0' + CAST(@date AS NVARCHAR(2)), 2);
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
		IF @TypeCode IS NOT NULL AND @TypeCode = 'ColomboTeaAuctionUSD'
		BEGIN
			INSERT INTO @TargetItems (ItemName) VALUES
				('Colombo Tea Auction Price');
		END
		ELSE IF @TypeCode IS NOT NULL AND @TypeCode = 'Imports(CIF)USD"'
		BEGIN
			INSERT INTO @TargetItems (ItemName) VALUES
				('Rice Imports'),
				('Sugar Imports'),
				('Wheat Imports'),
				('Crude Oil Imports');
		END
		ELSE IF @TypeCode IS NOT NULL AND @TypeCode = 'ColomboTeaAuctionLKR'
		BEGIN
			INSERT INTO @TargetItems (ItemName) VALUES
				('Colombo Tea Auction Price'); --Note: this item name is not in the data library, this key from tableau server
		END
		ELSE IF @TypeCode IS NOT NULL AND @TypeCode = 'Imports(CIF)LKR'
		BEGIN
			INSERT INTO @TargetItems (ItemName) VALUES
				('Rice Imports'),--Note: these item names are not in the data library, these keys from tableau server
				('Sugar Imports'),--Note: these item names are not in the data library, these keys from tableau server
				('Wheat Imports'),--Note: these item names are not in the data library, these keys from tableau server
				('Crude Oil Imports'); --Note: these item names are not in the data library, these keys from tableau server
		END
		
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


--================================================================
--d.1 Stored Procedure for Real GDP Growth Data (Daily EI) - National Accounts
--================================================================
CREATE OR ALTER PROCEDURE SP_GetRealGdpGrowth_ByPeriod
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

	IF @CurrentPeriod IS NULL
	BEGIN
		IF @YearCode IS NOT NULL
			SET @year = TRY_CAST(@YearCode AS INT);
		ELSE
			RETURN;
	END
	ELSE
	BEGIN
		SET @inputDate = TRY_CAST(@CurrentPeriod AS DATE);
		SET @year = YEAR(@inputDate);
		SET @month = MONTH(@inputDate);
		SET @date = DAY(@inputDate);
	END

	IF @FrequencyCode = 'A' SET @CurrentPeriod = CAST(@year AS NVARCHAR(4)) + '-00-00';
	IF @FrequencyCode = 'M' SET @CurrentPeriod = CAST(@year AS NVARCHAR(4)) + '-' + RIGHT('0' + CAST(@month AS NVARCHAR(2)), 2) + '-00';
	IF @FrequencyCode = 'D' SET @CurrentPeriod = CAST(@year AS NVARCHAR(4)) + '-' + RIGHT('0' + CAST(@month AS NVARCHAR(2)), 2) + '-' + RIGHT('0' + CAST(@date AS NVARCHAR(2)), 2);
	IF @FrequencyCode = 'Q' SET @CurrentPeriod = CAST(@year AS NVARCHAR(4)) + '-Q' + CAST(ISNULL(@QuarterCode, 1) AS NVARCHAR(1)) + '-00';

	DECLARE @TargetItems TABLE (ItemName NVARCHAR(200));
	INSERT INTO @TargetItems (ItemName) 
	VALUES ('GDP Growth - Constant (2015) Prices - Gross Domestic Product');

	SELECT dc.[Item Name] AS ItemName, dc.DataCodeID, dv.PeriodId, dv.Value AS CurrentValue
	FROM DataCode dc
	INNER JOIN DataValue dv ON dv.DataCodeID = dc.DataCodeID
	INNER JOIN @TargetItems ti ON ti.ItemName = dc.[Item Name]
	WHERE dv.PeriodID = @CurrentPeriod;
END;
GO;

--================================================================
--d.2 Stored Procedure for YoY Growth Data (Daily EI) - Prices and Indices 
--================================================================
CREATE OR ALTER PROCEDURE SP_GetPricesAndIndices_ByPeriod
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

	IF @CurrentPeriod IS NULL
	BEGIN
		IF @YearCode IS NOT NULL SET @year = TRY_CAST(@YearCode AS INT); ELSE RETURN;
	END
	ELSE
	BEGIN
		SET @inputDate = TRY_CAST(@CurrentPeriod AS DATE);
		SET @year = YEAR(@inputDate);
		SET @month = MONTH(@inputDate);
		SET @date = DAY(@inputDate);
	END

	IF @FrequencyCode = 'A' SET @CurrentPeriod = CAST(@year AS NVARCHAR(4)) + '-00-00';
	IF @FrequencyCode = 'M' SET @CurrentPeriod = CAST(@year AS NVARCHAR(4)) + '-' + RIGHT('0' + CAST(@month AS NVARCHAR(2)), 2) + '-00';
	IF @FrequencyCode = 'D' SET @CurrentPeriod = CAST(@year AS NVARCHAR(4)) + '-' + RIGHT('0' + CAST(@month AS NVARCHAR(2)), 2) + '-' + RIGHT('0' + CAST(@date AS NVARCHAR(2)), 2);
	IF @FrequencyCode = 'Q' SET @CurrentPeriod = CAST(@year AS NVARCHAR(4)) + '-Q' + CAST(ISNULL(@QuarterCode, 1) AS NVARCHAR(1)) + '-00';

	DECLARE @TargetItems TABLE (ItemName NVARCHAR(200));
	INSERT INTO @TargetItems (ItemName) VALUES
		('CCPI(2021=100)(Headline)(Year-on-Year %)'),
		('NCPI(2021=100)(Headline)(Year-on-Year %)');

	SELECT dc.[Item Name] AS ItemName, dc.DataCodeID, dv.PeriodId, dv.Value AS CurrentValue
	FROM DataCode dc
	INNER JOIN DataValue dv ON dv.DataCodeID = dc.DataCodeID
	INNER JOIN @TargetItems ti ON ti.ItemName = dc.[Item Name]
	WHERE dv.PeriodID = @CurrentPeriod;
END;
GO;


--================================================================
--d.3 Stored Procedure for TT  Data (Daily EI) - Exchange Rates
--================================================================
CREATE OR ALTER PROCEDURE SP_GetTTRate_ByPeriod
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

	IF @CurrentPeriod IS NULL
	BEGIN
		IF @YearCode IS NOT NULL SET @year = TRY_CAST(@YearCode AS INT); ELSE RETURN;
	END
	ELSE
	BEGIN
		SET @inputDate = TRY_CAST(@CurrentPeriod AS DATE);
		SET @year = YEAR(@inputDate);
		SET @month = MONTH(@inputDate);
		SET @date = DAY(@inputDate);
	END

	IF @FrequencyCode = 'A' SET @CurrentPeriod = CAST(@year AS NVARCHAR(4)) + '-00-00';
	IF @FrequencyCode = 'M' SET @CurrentPeriod = CAST(@year AS NVARCHAR(4)) + '-' + RIGHT('0' + CAST(@month AS NVARCHAR(2)), 2) + '-00';
	IF @FrequencyCode = 'D' SET @CurrentPeriod = CAST(@year AS NVARCHAR(4)) + '-' + RIGHT('0' + CAST(@month AS NVARCHAR(2)), 2) + '-' + RIGHT('0' + CAST(@date AS NVARCHAR(2)), 2);
	IF @FrequencyCode = 'Q' SET @CurrentPeriod = CAST(@year AS NVARCHAR(4)) + '-Q' + CAST(ISNULL(@QuarterCode, 1) AS NVARCHAR(1)) + '-00';

	DECLARE @TargetItems TABLE (ItemName NVARCHAR(200));
	INSERT INTO @TargetItems (ItemName) VALUES 
	('TT Rates -Buying USD'),
	('TT Rates -Buying GBP'),
	('TT Rates -Buying EUR'),
	('TT Rates -Buying JPY'),
	('TT Rates -Selling USD'),
	('TT Rates -Selling GBP'),
	('TT Rates -Selling EUR'),
	('TT Rates -Selling JPY'); 

	SELECT dc.[Item Name] AS ItemName, dc.DataCodeID, dv.PeriodId, dv.Value AS CurrentValue
	FROM DataCode dc
	INNER JOIN DataValue dv ON dv.DataCodeID = dc.DataCodeID
	INNER JOIN @TargetItems ti ON ti.ItemName = dc.[Item Name]
	WHERE dv.PeriodID = @CurrentPeriod;
END;
GO;

--================================================================
--d.4 Stored Procedure for Money Supply Data (Daily EI) -  Money Supply
--================================================================
CREATE OR ALTER PROCEDURE SP_GetMonySupply_ByPeriod
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

	IF @CurrentPeriod IS NULL
	BEGIN
		IF @YearCode IS NOT NULL SET @year = TRY_CAST(@YearCode AS INT); ELSE RETURN;
	END
	ELSE
	BEGIN
		SET @inputDate = TRY_CAST(@CurrentPeriod AS DATE);
		SET @year = YEAR(@inputDate);
		SET @month = MONTH(@inputDate);
		SET @date = DAY(@inputDate);
	END

	IF @FrequencyCode = 'A' SET @CurrentPeriod = CAST(@year AS NVARCHAR(4)) + '-00-00';
	IF @FrequencyCode = 'M' SET @CurrentPeriod = CAST(@year AS NVARCHAR(4)) + '-' + RIGHT('0' + CAST(@month AS NVARCHAR(2)), 2) + '-00';
	IF @FrequencyCode = 'D' SET @CurrentPeriod = CAST(@year AS NVARCHAR(4)) + '-' + RIGHT('0' + CAST(@month AS NVARCHAR(2)), 2) + '-' + RIGHT('0' + CAST(@date AS NVARCHAR(2)), 2);
	IF @FrequencyCode = 'Q' SET @CurrentPeriod = CAST(@year AS NVARCHAR(4)) + '-Q' + CAST(ISNULL(@QuarterCode, 1) AS NVARCHAR(1)) + '-00';

	DECLARE @TargetItems TABLE (ItemName NVARCHAR(200));
	INSERT INTO @TargetItems (ItemName) VALUES 
	('Currency in Circulation'),
	('Reserve Money (Daily Estimate)');

	SELECT dc.[Item Name] AS ItemName, dc.DataCodeID, dv.PeriodId, dv.Value AS CurrentValue
	FROM DataCode dc
	INNER JOIN DataValue dv ON dv.DataCodeID = dc.DataCodeID
	INNER JOIN @TargetItems ti ON ti.ItemName = dc.[Item Name]
	WHERE dv.PeriodID = @CurrentPeriod;
END;
GO;


--================================================================
--d.5 Stored Procedure for USD Spot Rate Data (Daily EI) - Exchange Rates
--================================================================
CREATE OR ALTER PROCEDURE SP_GetOpenMarketOperations_ByPeriod
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

	IF @CurrentPeriod IS NULL
	BEGIN
		IF @YearCode IS NOT NULL SET @year = TRY_CAST(@YearCode AS INT); ELSE RETURN;
	END
	ELSE
	BEGIN
		SET @inputDate = TRY_CAST(@CurrentPeriod AS DATE);
		SET @year = YEAR(@inputDate);
		SET @month = MONTH(@inputDate);
		SET @date = DAY(@inputDate);
	END

	IF @FrequencyCode = 'A' SET @CurrentPeriod = CAST(@year AS NVARCHAR(4)) + '-00-00';
	IF @FrequencyCode = 'M' SET @CurrentPeriod = CAST(@year AS NVARCHAR(4)) + '-' + RIGHT('0' + CAST(@month AS NVARCHAR(2)), 2) + '-00';
	IF @FrequencyCode = 'D' SET @CurrentPeriod = CAST(@year AS NVARCHAR(4)) + '-' + RIGHT('0' + CAST(@month AS NVARCHAR(2)), 2) + '-' + RIGHT('0' + CAST(@date AS NVARCHAR(2)), 2);
	IF @FrequencyCode = 'Q' SET @CurrentPeriod = CAST(@year AS NVARCHAR(4)) + '-Q' + CAST(ISNULL(@QuarterCode, 1) AS NVARCHAR(1)) + '-00';

	DECLARE @TargetItems TABLE (ItemName NVARCHAR(200));
	INSERT INTO @TargetItems (ItemName) VALUES ('USD Spot Rate');

	SELECT dc.[Item Name] AS ItemName, dc.DataCodeID, dv.PeriodId, dv.Value AS CurrentValue
	FROM DataCode dc
	INNER JOIN DataValue dv ON dv.DataCodeID = dc.DataCodeID
	INNER JOIN @TargetItems ti ON ti.ItemName = dc.[Item Name]
	WHERE dv.PeriodID = @CurrentPeriod;
END;
GO;

--================================================================
--d.6 Stored Procedure for Policy Rates Data (Daily EI) - Policy Rates
--================================================================
CREATE OR ALTER PROCEDURE SP_GetPolicyRates_ByPeriod
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

	IF @CurrentPeriod IS NULL
	BEGIN
		IF @YearCode IS NOT NULL SET @year = TRY_CAST(@YearCode AS INT); ELSE RETURN;
	END
	ELSE
	BEGIN
		SET @inputDate = TRY_CAST(@CurrentPeriod AS DATE);
		SET @year = YEAR(@inputDate);
		SET @month = MONTH(@inputDate);
		SET @date = DAY(@inputDate);
	END

	IF @FrequencyCode = 'A' SET @CurrentPeriod = CAST(@year AS NVARCHAR(4)) + '-00-00';
	IF @FrequencyCode = 'M' SET @CurrentPeriod = CAST(@year AS NVARCHAR(4)) + '-' + RIGHT('0' + CAST(@month AS NVARCHAR(2)), 2) + '-00';
	IF @FrequencyCode = 'D' SET @CurrentPeriod = CAST(@year AS NVARCHAR(4)) + '-' + RIGHT('0' + CAST(@month AS NVARCHAR(2)), 2) + '-' + RIGHT('0' + CAST(@date AS NVARCHAR(2)), 2);
	IF @FrequencyCode = 'Q' SET @CurrentPeriod = CAST(@year AS NVARCHAR(4)) + '-Q' + CAST(ISNULL(@QuarterCode, 1) AS NVARCHAR(1)) + '-00';

	DECLARE @TargetItems TABLE (ItemName NVARCHAR(200));
	INSERT INTO @TargetItems (ItemName) VALUES
		('Open Market Operations'),
		('Policy Rates - Overnight Policy Rate (OPR)');

	SELECT dc.[Item Name] AS ItemName, dc.DataCodeID, dv.PeriodId, dv.Value AS CurrentValue
	FROM DataCode dc
	INNER JOIN DataValue dv ON dv.DataCodeID = dc.DataCodeID
	INNER JOIN @TargetItems ti ON ti.ItemName = dc.[Item Name]
	WHERE dv.PeriodID = @CurrentPeriod;
END;
GO;

--================================================================
--d.7 Stored Procedure for AWPR Data (Daily EI) - Interest Rates
--================================================================
CREATE OR ALTER PROCEDURE SP_GetAwpr_ByPeriod
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

	IF @CurrentPeriod IS NULL
	BEGIN
		IF @YearCode IS NOT NULL SET @year = TRY_CAST(@YearCode AS INT); ELSE RETURN;
	END
	ELSE
	BEGIN
		SET @inputDate = TRY_CAST(@CurrentPeriod AS DATE);
		SET @year = YEAR(@inputDate);
		SET @month = MONTH(@inputDate);
		SET @date = DAY(@inputDate);
	END

	IF @FrequencyCode = 'A' SET @CurrentPeriod = CAST(@year AS NVARCHAR(4)) + '-00-00';
	IF @FrequencyCode = 'M' SET @CurrentPeriod = CAST(@year AS NVARCHAR(4)) + '-' + RIGHT('0' + CAST(@month AS NVARCHAR(2)), 2) + '-00';
	IF @FrequencyCode = 'D' SET @CurrentPeriod = CAST(@year AS NVARCHAR(4)) + '-' + RIGHT('0' + CAST(@month AS NVARCHAR(2)), 2) + '-' + RIGHT('0' + CAST(@date AS NVARCHAR(2)), 2);
	IF @FrequencyCode = 'Q' SET @CurrentPeriod = CAST(@year AS NVARCHAR(4)) + '-Q' + CAST(ISNULL(@QuarterCode, 1) AS NVARCHAR(1)) + '-00';

	DECLARE @TargetItems TABLE (ItemName NVARCHAR(200));
	INSERT INTO @TargetItems (ItemName) VALUES
		('Average Weighted Prime Lending Rate (AWPR) - Weekly'),
		('Average Weighted Call Money Rate'),
		('Market Repo - Weighted Average Rate');

	SELECT dc.[Item Name] AS ItemName, dc.DataCodeID, dv.PeriodId, dv.Value AS CurrentValue
	FROM DataCode dc
	INNER JOIN DataValue dv ON dv.DataCodeID = dc.DataCodeID
	INNER JOIN @TargetItems ti ON ti.ItemName = dc.[Item Name]
	WHERE dv.PeriodID = @CurrentPeriod;
END;
GO;

--================================================================
--d.8 Stored Procedure for Overnight Liquidity Data (Daily EI) - Overnight Liquidity
--================================================================
CREATE OR ALTER PROCEDURE SP_GetOvernightLiquidity_ByPeriod
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

	IF @CurrentPeriod IS NULL
	BEGIN
		IF @YearCode IS NOT NULL SET @year = TRY_CAST(@YearCode AS INT); ELSE RETURN;
	END
	ELSE
	BEGIN
		SET @inputDate = TRY_CAST(@CurrentPeriod AS DATE);
		SET @year = YEAR(@inputDate);
		SET @month = MONTH(@inputDate);
		SET @date = DAY(@inputDate);
	END

	IF @FrequencyCode = 'A' SET @CurrentPeriod = CAST(@year AS NVARCHAR(4)) + '-00-00';
	IF @FrequencyCode = 'M' SET @CurrentPeriod = CAST(@year AS NVARCHAR(4)) + '-' + RIGHT('0' + CAST(@month AS NVARCHAR(2)), 2) + '-00';
	IF @FrequencyCode = 'D' SET @CurrentPeriod = CAST(@year AS NVARCHAR(4)) + '-' + RIGHT('0' + CAST(@month AS NVARCHAR(2)), 2) + '-' + RIGHT('0' + CAST(@date AS NVARCHAR(2)), 2);
	IF @FrequencyCode = 'Q' SET @CurrentPeriod = CAST(@year AS NVARCHAR(4)) + '-Q' + CAST(ISNULL(@QuarterCode, 1) AS NVARCHAR(1)) + '-00';

	DECLARE @TargetItems TABLE (ItemName NVARCHAR(200));
	INSERT INTO @TargetItems (ItemName) VALUES 
	('Overnight Liquidity (Injection (-) / Absorption (+))');

	SELECT dc.[Item Name] AS ItemName, dc.DataCodeID, dv.PeriodId, dv.Value AS CurrentValue
	FROM DataCode dc
	INNER JOIN DataValue dv ON dv.DataCodeID = dc.DataCodeID
	INNER JOIN @TargetItems ti ON ti.ItemName = dc.[Item Name]
	WHERE dv.PeriodID = @CurrentPeriod;
END;
GO;

--================================================================
--d.9 Stored Procedure for Treasury Bills Data (Daily EI) - Treasury Bills
--================================================================
CREATE OR ALTER PROCEDURE SP_GetTreasuryBillYield_ByPeriod
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

	IF @CurrentPeriod IS NULL
	BEGIN
		IF @YearCode IS NOT NULL SET @year = TRY_CAST(@YearCode AS INT); ELSE RETURN;
	END
	ELSE
	BEGIN
		SET @inputDate = TRY_CAST(@CurrentPeriod AS DATE);
		SET @year = YEAR(@inputDate);
		SET @month = MONTH(@inputDate);
		SET @date = DAY(@inputDate);
	END

	IF @FrequencyCode = 'A' SET @CurrentPeriod = CAST(@year AS NVARCHAR(4)) + '-00-00';
	IF @FrequencyCode = 'M' SET @CurrentPeriod = CAST(@year AS NVARCHAR(4)) + '-' + RIGHT('0' + CAST(@month AS NVARCHAR(2)), 2) + '-00';
	IF @FrequencyCode = 'D' SET @CurrentPeriod = CAST(@year AS NVARCHAR(4)) + '-' + RIGHT('0' + CAST(@month AS NVARCHAR(2)), 2) + '-' + RIGHT('0' + CAST(@date AS NVARCHAR(2)), 2);
	IF @FrequencyCode = 'Q' SET @CurrentPeriod = CAST(@year AS NVARCHAR(4)) + '-Q' + CAST(ISNULL(@QuarterCode, 1) AS NVARCHAR(1)) + '-00';

	DECLARE @TargetItems TABLE (ItemName NVARCHAR(200));
	INSERT INTO @TargetItems (ItemName) VALUES 
	('Treasury Bill Primary Market Auction Weighted Average Yield Rate -91 days'),
	('Treasury Bill Primary Market Auction Weighted Average Yield Rate -182 days'),
	('Treasury Bill Primary Market Auction Weighted Average Yield Rate -364 days'),
	('Secondary Market Yield Rates of T-Bills - 91 day'),
	('Secondary Market Yield Rates of T-Bills - 182 day'),
	('Secondary Market Yield Rates of T-Bills - 364 day');

	SELECT dc.[Item Name] AS ItemName, dc.DataCodeID, dv.PeriodId, dv.Value AS CurrentValue
	FROM DataCode dc
	INNER JOIN DataValue dv ON dv.DataCodeID = dc.DataCodeID
	INNER JOIN @TargetItems ti ON ti.ItemName = dc.[Item Name]
	WHERE dv.PeriodID = @CurrentPeriod;
END;
GO;

--================================================================
--d.10 Stored Procedure for Share Market Data (Daily EI) - Share Market Data
--================================================================
CREATE OR ALTER PROCEDURE SP_GetAllSharePriceIndex_ByPeriod
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

	IF @CurrentPeriod IS NULL
	BEGIN
		IF @YearCode IS NOT NULL SET @year = TRY_CAST(@YearCode AS INT); ELSE RETURN;
	END
	ELSE
	BEGIN
		SET @inputDate = TRY_CAST(@CurrentPeriod AS DATE);
		SET @year = YEAR(@inputDate);
		SET @month = MONTH(@inputDate);
		SET @date = DAY(@inputDate);
	END

	IF @FrequencyCode = 'A' SET @CurrentPeriod = CAST(@year AS NVARCHAR(4)) + '-00-00';
	IF @FrequencyCode = 'M' SET @CurrentPeriod = CAST(@year AS NVARCHAR(4)) + '-' + RIGHT('0' + CAST(@month AS NVARCHAR(2)), 2) + '-00';
	IF @FrequencyCode = 'D' SET @CurrentPeriod = CAST(@year AS NVARCHAR(4)) + '-' + RIGHT('0' + CAST(@month AS NVARCHAR(2)), 2) + '-' + RIGHT('0' + CAST(@date AS NVARCHAR(2)), 2);
	IF @FrequencyCode = 'Q' SET @CurrentPeriod = CAST(@year AS NVARCHAR(4)) + '-Q' + CAST(ISNULL(@QuarterCode, 1) AS NVARCHAR(1)) + '-00';

	DECLARE @TargetItems TABLE (ItemName NVARCHAR(200));
	INSERT INTO @TargetItems (ItemName) VALUES 
	('EQUITY- All share price index'),
	('EQUITY-S&P SL20 Index'),
	('EQUITY- Daily Turnover'),
	('EQUITY-Market Capitalization'),
	('EQUITY- Market Price Earnings Ratio'),
	('EQUITY- Foreign Purchases'),
	('EQUITY- Foreign Sales');

	SELECT dc.[Item Name] AS ItemName, dc.DataCodeID, dv.PeriodId, dv.Value AS CurrentValue
	FROM DataCode dc
	INNER JOIN DataValue dv ON dv.DataCodeID = dc.DataCodeID
	INNER JOIN @TargetItems ti ON ti.ItemName = dc.[Item Name]
	WHERE dv.PeriodID = @CurrentPeriod;
END;
GO;

--================================================================
--d.11 Stored Procedure for Petroleum Data (Daily EI) - Petroleum
--================================================================
CREATE OR ALTER PROCEDURE SP_GetPetroleum_ByPeriod
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

	IF @CurrentPeriod IS NULL
	BEGIN
		IF @YearCode IS NOT NULL SET @year = TRY_CAST(@YearCode AS INT); ELSE RETURN;
	END
	ELSE
	BEGIN
		SET @inputDate = TRY_CAST(@CurrentPeriod AS DATE);
		SET @year = YEAR(@inputDate);
		SET @month = MONTH(@inputDate);
		SET @date = DAY(@inputDate);
	END

	IF @FrequencyCode = 'A' SET @CurrentPeriod = CAST(@year AS NVARCHAR(4)) + '-00-00';
	IF @FrequencyCode = 'M' SET @CurrentPeriod = CAST(@year AS NVARCHAR(4)) + '-' + RIGHT('0' + CAST(@month AS NVARCHAR(2)), 2) + '-00';
	IF @FrequencyCode = 'D' SET @CurrentPeriod = CAST(@year AS NVARCHAR(4)) + '-' + RIGHT('0' + CAST(@month AS NVARCHAR(2)), 2) + '-' + RIGHT('0' + CAST(@date AS NVARCHAR(2)), 2);
	IF @FrequencyCode = 'Q' SET @CurrentPeriod = CAST(@year AS NVARCHAR(4)) + '-Q' + CAST(ISNULL(@QuarterCode, 1) AS NVARCHAR(1)) + '-00';

	DECLARE @TargetItems TABLE (ItemName NVARCHAR(200));
	INSERT INTO @TargetItems (ItemName) VALUES
	('CPC Local Petroleum Prices - Petrol'),
	('CPC Local Petroleum Prices - Diesel'),
	('CPC Local Petroleum Prices - Kerosene'),
	('Crude Oil Futures Prices - Brent (Benchmark price)'),
	('Crude Oil Futures Prices - WTI (Benchmark price)'),
	('Crude Oil Prices - OPEC'),
	('International Petroleum Prices - Refined Products - Singapore Market - Petrol'),
	('International Petroleum Prices - Refined Products - Singapore Market - Diesel'),
	('International Petroleum Prices - Refined Products - Singapore Market - Kerosene');



	SELECT dc.[Item Name] AS ItemName, dc.DataCodeID, dv.PeriodId, dv.Value AS CurrentValue
	FROM DataCode dc
	INNER JOIN DataValue dv ON dv.DataCodeID = dc.DataCodeID
	INNER JOIN @TargetItems ti ON ti.ItemName = dc.[Item Name]
	WHERE dv.PeriodID = @CurrentPeriod;
END;
GO;

--================================================================
--d.12 Stored Procedure for Electricity Data (Daily EI) - Electricity
--================================================================
CREATE OR ALTER PROCEDURE SP_GetElectricity_ByPeriod
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

	IF @CurrentPeriod IS NULL
	BEGIN
		IF @YearCode IS NOT NULL SET @year = TRY_CAST(@YearCode AS INT); ELSE RETURN;
	END
	ELSE
	BEGIN
		SET @inputDate = TRY_CAST(@CurrentPeriod AS DATE);
		SET @year = YEAR(@inputDate);
		SET @month = MONTH(@inputDate);
		SET @date = DAY(@inputDate);
	END

	IF @FrequencyCode = 'A' SET @CurrentPeriod = CAST(@year AS NVARCHAR(4)) + '-00-00';
	IF @FrequencyCode = 'M' SET @CurrentPeriod = CAST(@year AS NVARCHAR(4)) + '-' + RIGHT('0' + CAST(@month AS NVARCHAR(2)), 2) + '-00';
	IF @FrequencyCode = 'D' SET @CurrentPeriod = CAST(@year AS NVARCHAR(4)) + '-' + RIGHT('0' + CAST(@month AS NVARCHAR(2)), 2) + '-' + RIGHT('0' + CAST(@date AS NVARCHAR(2)), 2);
	IF @FrequencyCode = 'Q' SET @CurrentPeriod = CAST(@year AS NVARCHAR(4)) + '-Q' + CAST(ISNULL(@QuarterCode, 1) AS NVARCHAR(1)) + '-00';

	DECLARE @TargetItems TABLE (ItemName NVARCHAR(200));
	INSERT INTO @TargetItems (ItemName) VALUES
	('Electricity Generation - Total Energy'),
	('Electricity Generation - Peak Demand'),
	('Electricity Generation - Thermal Coal'),
	('Electricity Generation - Thermal Oil'),
	('Electricity Generation - Hydro'),
	('Electricity Generation - Wind'),
	('Electricity Generation - Solar'),
	('Electricity Generation - Biomass');


	SELECT dc.[Item Name] AS ItemName, dc.DataCodeID, dv.PeriodId, dv.Value AS CurrentValue
	FROM DataCode dc
	INNER JOIN DataValue dv ON dv.DataCodeID = dc.DataCodeID
	INNER JOIN @TargetItems ti ON ti.ItemName = dc.[Item Name]
	WHERE dv.PeriodID = @CurrentPeriod;
END;
GO;

