# Sunsynk Solar Investment Calculator

A .NET console application that calculates how much money you've saved by generating solar power, using real data from the Sunsynk API and City of Tshwane electricity tariffs.

## Features

- Fetches monthly solar generation data from the Sunsynk API
- Applies City of Tshwane inclining block tariffs (2021/22 - 2025/26)
- Caches API data in SQLite to avoid redundant API calls
- Displays a yearly breakdown of PV generation and savings in Rands
- Calculates investment payoff timeline based on actual and projected savings

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- A Sunsynk Connect account (https://sunsynk.net)

## Setup

1. Clone the repository

2. Copy the example config and fill in your credentials:
   ```bash
   cp src/SunsynkInvestment/appsettings.example.json src/SunsynkInvestment/appsettings.json
   ```

3. Edit `src/SunsynkInvestment/appsettings.json`:
   ```json
   {
     "Sunsynk": {
       "Username": "your-email@example.com",
       "Password": "your-sunsynk-password",
       "PlantId": 308360
     },
     "TotalInvestmentAmount": 150000,
     "Database": {
       "ConnectionString": "Data Source=sunsynk_investment.db"
     }
   }
   ```

   | Setting | Description |
   |---------|-------------|
   | `Username` | Your Sunsynk Connect email |
   | `Password` | Your Sunsynk Connect password |
   | `PlantId` | Your plant ID (visible in the Sunsynk Connect URL) |
   | `TotalInvestmentAmount` | Total cost of your solar installation in Rands. Set to `0` to skip payoff calculation. |

## Usage

```bash
cd src/SunsynkInvestment
dotnet run
```

On first run the app will:
1. Create the SQLite database and seed tariff data
2. Authenticate with the Sunsynk API
3. Fetch monthly energy data for each year (2022 onwards)
4. Display the savings report

Subsequent runs use cached data for completed years. Current year data refreshes after 24 hours.

### Refreshing all data

Delete the database file to force a full re-fetch:
```bash
rm src/SunsynkInvestment/sunsynk_investment.db
dotnet run
```

## Tariffs

City of Tshwane residential electricity tariffs (excl. VAT) are stored in SQLite and seeded on first run:

| Financial Year | 0-100 kWh | 101-400 kWh | 401-650 kWh | >650 kWh |
|---|---|---|---|---|
| 2021/22 | R1.9519 | R2.2844 | R2.4881 | R2.6823 |
| 2022/23 | R2.0970 | R2.4541 | R2.6738 | R2.8824 |
| 2023/24 | R2.4137 | R2.8247 | R3.0775 | R3.3176 |
| 2024/25 | R2.7033 | R3.1637 | R3.4468 | R3.7158 |
| 2025/26 | R2.9790 | R3.4864 | R3.7983 | R4.0948 |

Financial years run July to June. To add future tariffs, delete the database and update the seed data in `Data/DatabaseInitializer.cs`.

## Project Structure

```
src/SunsynkInvestment/
  Program.cs                    # Entry point and report display
  Configuration/
    SunsynkSettings.cs          # Config POCO
  Models/
    SunsynkAuthResponse.cs      # Auth API response
    SunsynkEnergyResponse.cs    # Energy API response
    MonthlyEnergy.cs            # Cached energy record
    Tariff.cs                   # Tariff rates
  Services/
    SunsynkApiService.cs        # Auth (RSA) + API data fetching
    SavingsCalculator.cs        # Block tariff calculation
  Data/
    DatabaseInitializer.cs      # Schema creation + tariff seeding
    Repositories.cs             # SQLite data access
```
