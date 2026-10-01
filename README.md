# CosmosDbQueryTool

A small WPF desktop app for running Azure Cosmos DB SQL queries and inspecting the results.

## Features

- Enter Cosmos DB `Endpoint`, `Primary Key`, `Database`, and `Container`
- Keep multiple connection profiles in tabs inside a dedicated Connections window
- Write and run Cosmos DB SQL queries
- Show returned rows in a results grid
- Select any row to view its full JSON in a separate detail pane
- Limit the number of returned rows with `Max Items`
- Reuse the last 50 successful unique queries from the recent-query drop-down
- Save successful connection details locally so they are loaded the next time the app starts
- The selected tab in the Connections window is the active connection used by the main window

## Project structure

- `CosmosDbQueryTool.slnx`
- `CosmosDbQueryTool/` - WPF application

## Build

```powershell
Set-Location "C:\_DEV_\Cosmos_Db"
dotnet build .\CosmosDbQueryTool.slnx
```

## Run

```powershell
Set-Location "C:\_DEV_\Cosmos_Db"
dotnet run --project .\CosmosDbQueryTool\CosmosDbQueryTool.csproj
```

## Usage

1. Start the app.
2. Enter your Cosmos DB endpoint and primary key.
3. Enter the database and container names.
4. Click **Open Connections** to manage connection tabs.
5. Use **+ New Connection** in the Connections window if you need another profile.
6. Write a query such as:

```sql
SELECT TOP 25 * FROM c
```

7. Click **Run Query**.
8. Review the returned rows in the list.
9. Click a row to inspect the full JSON document in the detail view.
10. When a query returns rows, it is added to the recent-query list for later reuse.

## Notes

- The app uses the `Microsoft.Azure.Cosmos` SDK.
- Queries run directly against the specified container.
- Network access and valid Cosmos DB credentials are required at runtime.


