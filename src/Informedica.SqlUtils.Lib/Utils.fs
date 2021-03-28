namespace Informedica.SqlUtils.Lib

module Env =

    open System
    open System.IO
    open System.Linq
    open System.Collections.Generic


    /// Recursively tries to find the parent of a file starting from a directory
    let rec findParent (directory: string) (fileToFind: string) =
        let path =
            if Directory.Exists(directory) then directory else Directory.GetParent(directory).FullName

        let files = Directory.GetFiles(path)
        if files.Any(fun file -> Path.GetFileName(file).ToLower() = fileToFind.ToLower())
        then path
        else findParent (DirectoryInfo(path).Parent.FullName) fileToFind


    /// Returns enviroment variables as a dictionary
    let environmentVars() =
        let variables = Dictionary<string, string>()
        let userVariables = Environment.GetEnvironmentVariables(EnvironmentVariableTarget.User)
        let processVariables = Environment.GetEnvironmentVariables(EnvironmentVariableTarget.Process)
        for pair in userVariables do
            let variable = unbox<Collections.DictionaryEntry> pair
            let key = unbox<string> variable.Key
            let value = unbox<string> variable.Value
            if not (variables.ContainsKey(key)) && key <> "PATH" then variables.Add(key, value)
        for pair in processVariables do
            let variable = unbox<Collections.DictionaryEntry> pair
            let key = unbox<string> variable.Key
            let value = unbox<string> variable.Value
            if not (variables.ContainsKey(key)) && key <> "PATH" then variables.Add(key, value)
        variables

    let getItem s =
        let vars = environmentVars()
        if not (vars.ContainsKey(s)) then None
        else
            vars.Item(s)
            |> Some


module Utils =

    open System
    open System.Data
    open Microsoft.Data.SqlClient

    let pidSql = """
        SELECT spid, loginame FROM sys.sysprocesses
        WHERE DB_NAME(dbid)='model'
    """

    let isConnectionString connstr =
        try
            let connstr = SqlConnectionStringBuilder(connstr)
            connstr.ConnectTimeout <- 5

            (new SqlConnection(connstr.ConnectionString)).Open()
            true
        with
        | _ -> false


    let connStringToSetting connstr =
        if isConnectionString connstr then
            let builder = SqlConnectionStringBuilder(connstr)
            (builder.DataSource, builder.InitialCatalog, builder.UserID, builder.Password) |> Some
        else None


    let createConnectionString server db user password timeout =
        let builder = SqlConnectionStringBuilder()

        builder.DataSource <- if String.IsNullOrWhiteSpace(server) then "(local)" else server
        builder.InitialCatalog <- db
        if String.IsNullOrWhiteSpace(user) |> not &&
           String.IsNullOrWhiteSpace(password) |> not then builder.UserID <- user; builder.Password <- password
                                        else builder.IntegratedSecurity <- true

        builder.ConnectTimeout <- timeout
        builder.ConnectionString


    let createSqlConnection (database: string) (timeout: int) =
        let connstr = createConnectionString "" database "" "" timeout
        let conn = new SqlConnection(connstr)
        conn


    let executeQuery qry connstr =
        use conn = new SqlConnection(connstr)
        conn.Open()
        let cmd = new SqlCommand(qry, conn)
        cmd.ExecuteReader CommandBehavior.Default


    let executeNonQuery qry (conn: SqlConnection)  =
        if conn.State <> ConnectionState.Open then conn.Open()

        use trans = conn.BeginTransaction()
        use command = new SqlCommand(qry, trans.Connection)
        command.Transaction <- trans
        command.CommandTimeout <- conn.ConnectionTimeout
//        printfn "Starting Query:\n%s\nWith connection:%s" qry (conn.ConnectionString)

        try
            match command.ExecuteNonQuery() with
            | 0 -> trans.Rollback(); conn.Close(); (false, "")
            | _ -> trans.Commit();   conn.Close(); (true, "")
        with
            | e -> trans.Rollback(); conn.Close(); (false, e.ToString())


    let runSqlForDb dbname (sql: string) =
        let conn = createSqlConnection dbname 6000

        let sql = sql.Split([|"GO"|], StringSplitOptions.RemoveEmptyEntries)
        sql |> Array.iter(fun s ->  let r, e = executeNonQuery s conn
                                    if r |> not then
                                        printfn "This query did not run:\n%s\n Error:\n%s" s e)

