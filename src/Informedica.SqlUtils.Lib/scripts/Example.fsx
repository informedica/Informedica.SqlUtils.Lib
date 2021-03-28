
#r "nuget: Microsoft.Data.SqlClient"

#load "../Utils.fs"
#load "../RowReader.fs"
#load "../Sql.fs"

open Informedica.SqlUtils.Lib


"PICURED_CONNSTR"
|> Env.getItem
|> function
| Some connStr ->

    let patients  =
        connStr
        |> Sql.connect
        |> Sql.query "select top 100 * from Data"
        |> Sql.execute (fun read ->
            {|
                Id = read.string "pat_hosp_id"
            |}
        )
    patients
    |> function
    | Ok xs -> xs |> Seq.iter (printfn "%A")
    | Error e -> printfn "%A" e
| None -> ()
