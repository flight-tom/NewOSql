# NewOSql
A replacement for old oSQL.exe with SQL server

## Introduce

oSQL.exe was a tiny tool for executing sql script in CLI. Unfortunately, it doesn't support unicode and UTF8 files...

This is the reason why I make this one for supporting unicode.

### How to use
```
**oSQL.exe**
 License: Apache 2.0
 Author: Tom Tang <tomtang0406@gmail.com>
==========================================
Usage:
Usage:
oSQL.exe -s [Server IP] [-is:use integrated security| -u <account> -p <password>] -o [log file path] [-i <sql script file path> | -dir <folder path contains sql files>] [-renew: drop destination database and re-create] -d [destination database] -e [export file path]

Sample:
OSQL.exe -S ./SQLEXPRESS -U sa -P p@ssw0rd  -o .\CPBU_SQLDEPLOY.LOG -i database\10_tables\00.table_create.sql -d SampleDB -e .\excel.csv
OSQL.exe -s ./SQLEXPRESS -U sa -P p@ssw0rd  -o .\CPBU_SQLDEPLOY.LOG -i "database\10_tables\00.table_create.sql" -d SampleDB
OSQL.exe -is  -o .\log.log -i "database\10_tables\00.table_create.sql" -d SampleDB
OSQL.exe -is  -o .\log.log -dir "database" -renew -d SampleDB
```

Updates
-----------------------
**2020-10-28:**
We add one new feature that can export the query result to a new csv file.
By security concern, when you enable this argument, you can give it a file contains **SELECT** statement only, any others could cause data changing are not allowed.
AND **SELECT** must be the beginning in the file.

**2024-05-16:**

### New features: ###
- can use integrated security now, prevent from exposing authentication information.
- execute all sql files directly in recursive folders.
- rebuild db by dropping and creating.
- code refine into smaller pieces.
