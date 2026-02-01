docker run -e "ACCEPT_EULA=Y" \
  -e "MSSQL_SA_PASSWORD=268191Thiena@" \
  -p 1433:1433 \
  --name sql2022 \
  -d mcr.microsoft.com/mssql/server:2022-latest