
// NuGet
using Microsoft.AnalysisServices.AdomdClient;
using System.Data;

///////////////////////////////////////////////////////////////////////////////
// 01. AdomdConnection

string connStr = "DataSource=CORNFLOWER\\SQLSERVER_DEV;Initial Catalog=analysis_services_2022_with_chatgpt";

using (var conn = new AdomdConnection(connStr))
{
  conn.Open();

  // connected to: SSAS

  ///////////////////////////////////////////////////////////////////////////////
  // 02. CubeDef -> metadata

  CubeDef cube = conn.Cubes["adventure_works_datacube"];

  Console.WriteLine(new string('*', 50));
  Console.WriteLine($"** Hierarchies in DIM: Due Date");

  foreach (Hierarchy h in cube.Dimensions["Due Date"].Hierarchies)
  {
    // hierarchies, not levels (level is like in statistics, specific value)

    // both Attribute Hierarchies (2d) and Hierarchies (User-Defined)
    Console.WriteLine(h.Name);
  }

  // Hierarchy (User-Defined)
  Hierarchy calendar = cube.Dimensions["Due Date"].Hierarchies["Calendar"];

  Console.WriteLine(new string('*', 50));
  Console.WriteLine($"** Hierarchy (User-Defined)");

  Console.WriteLine(calendar.ToString());

  //
  // AttributeHierarchy is 2d stucture created automatically
  // AttributeHierarchy is created for: each dimension
  // consist of: [All], &[2010], &[2011], &[2012], ..
  //
  // Hierarchy (User-Defined) is Nd structure created by analyst
  // defining "drill-down" table relationships (Year - Month - Day)
  // writing like: [Due Date].[Calendar].[Calendar Year].&[2005]
  // although there is no: [All]

  ///////////////////////////////////////////////////////////////////////////////
  // 03. AdomdCommand

  // executing MDX

  using (var cmd = conn.CreateCommand())
  {
    // multi-line string
    cmd.CommandText = @"
      select
        [Measures].[Sales Amount] on columns
        , [Due Date].[Calendar Year].Members on rows
      from [adventure_works_datacube];
    ";

    ///////////////////////////////////////////////////////////////////////////////
    // 04. AdomdDataReader

    using (var reader = cmd.ExecuteReader())
    {
      DataTable salesOverYears = new DataTable();

      //DataTable mdxSchema = reader.GetSchemaTable();

      //while (reader.Read())
      //{
      //  for (int i = 0; i < mdxSchema.Columns.Count; i++)
      //  {
      //    var columnName = reader[i].ToString();

      //    salesOverYears.Columns.Add(columnName.ToString());
      //  }
      //}

      // adding DataTable -> DataSet
      DataSet set = new DataSet();

      // to command DataTable that it is not relational
      set.EnforceConstraints = false;

      // later we can remove it
      set.Tables.Add(salesOverYears);

      // here relational constraints are NOT BEING CHECKED (good)
      salesOverYears.Load(reader);
      
      set.Tables.Remove(salesOverYears);

      // we now have: columns and rows (ADO.NET structure)
      // whats next: writing data to console

      Console.WriteLine(new string('*', 50));
      Console.WriteLine($"** AdomdConnection -> AdomdCommand -> AdomdDataReader -> DataTable");

      foreach (var col in salesOverYears.Columns)
      {
        Console.WriteLine($"*");
        Console.WriteLine($"column: {col.ToString()}");

        for (int i = 0; i < salesOverYears.Rows.Count; i++)
        {
          Console.WriteLine($"{i}: \t &{salesOverYears.Rows[i][col.ToString()].ToString()}");
        }
      }

      // data written to console

      ///////////////////////////////////////////////////////////////////////////////
      // 05. AdomdDataAdapter

      var adapter = new AdomdDataAdapter(cmd);

      var dataSet = new DataSet();

      adapter.Fill(dataSet, "salesOverYears");

      Console.WriteLine(new string('*', 50));
      Console.WriteLine($"** AdomdDataAdapter");

      Console.WriteLine($"DataSet tables count: {dataSet.Tables.Count.ToString()}");

      ///////////////////////////////////////////////////////////////////////////////
      // 06. CellSet: OLAP result (OnLine Analytical Processing

      // this preserves: dimensionality of data (we dont flatten into DataTable)

      // returns: CellSet (not AdomdDataReader)
      CellSet cellSet = cmd.ExecuteCellSet();

      TupleCollection columns = cellSet.Axes[0].Set.Tuples;
      TupleCollection rows = cellSet.Axes[1].Set.Tuples;

      // column tuple represents: axis 0
      // row    tuple represents: axis 1

      Console.WriteLine(new string('*', 50));
      Console.WriteLine($"** CellSet: NOT flattened DataTable");

      for (int r = 0; r < rows.Count; r++)
      {
        for (int c = 0; c < columns.Count; c++)
        {
          Cell cell = cellSet.Cells[c, r];
          Console.WriteLine($"&{cell.Value ?? ""}");
        }
      }

      //    0: Sales Amount (we dont even have any other Measures)
      // ,  7: Sales Amount in &[2011]
      Cell saleInYear = cellSet.Cells[0, 7];

      Console.WriteLine(new string('*', 50));
      Console.WriteLine($"** Cell: specific tuple (specific year x specific measure)");

      Console.WriteLine($"sales in year 2011: {saleInYear.Value}");
    } // reader
  }
}