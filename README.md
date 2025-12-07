# DBMetaTool - Jest to aplikacja konsolowa, która służy do eksportowania metadanych i aktualizowania istniejącej bazy danych Firebird, lub tworzenia kompletnie nowej bazy danych na podstawie skryptów

## Funkcjonalności:
- **BuildDatabase:** Służy do wygenerowania nowej bazy danych na podstawie wcześniej przygotowanych skryptów.  
  Przykład użycia:

  ```powershell
  'dotnet run -- build-db --db-dir ".\generatedDb" --scripts-dir ".\scripts"'

- **ExportScripts:** Służy do wygenerowania skryptów oraz metadanych z istniejącej bazy danych Firebird.  
  Przykład użycia:

  ```powershell
  'dotnet run -- export-scripts --connection-string "CONNECTION_STRING" --output-dir ".\scripts"'

- **UpdateDatabase:** Służy do aktualizacji istniejącej bazy danych za pomocą wcześniej przygotowanych skryptów.  
  Przykład użycia:

  ```powershell
  'dotnet run -- update-db --connection-string "CONNECTION_STRING" --scripts-dir ".\scripts"

## Wymagania
- .NET 8
- Firebird 5.0
- Plik config.json zawierający dane logowania do bazy danych

```json
{
  "User": "user_login"
  "Password": "user_password"
}
