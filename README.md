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
oSQL.exe -S [Server IP] -U [db account] -P [db password] -o [log file path] -i [sql script file path] -d [destination database] -e [csv file path]
Sample:
OSQL.EXE -S ./SQLEXPRESS -U sa -P p@ssw0rd  -o .\CPBU_SQLDEPLOY.LOG -i database\10_tables\00.table_create.sql -d SampleDB -e .\excel.csv
```

Notice
-----------------------
We add one new feature that can export the query result to a new csv file.
By security concern, when you enable this argument, you can give it a file contains "SELECT" statement only, any others could cause data changing are not allowed.
AND "SELECT" must be the beginning in the file.
