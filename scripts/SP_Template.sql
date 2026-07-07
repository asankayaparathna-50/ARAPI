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