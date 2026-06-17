using System.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sandbox.Core.Sql;

namespace Sandbox.Tests.Sql
{
    [TestClass]
    public class RowMaterializerTests
    {
        private static DataTable BuildTable()
        {
            var table = new DataTable();
            table.Columns.Add("Id", typeof(int));
            table.Columns.Add("Name", typeof(string));
            table.Columns.Add("Price", typeof(decimal));
            table.Rows.Add(1, "Bolt", 5.00m);
            table.Rows.Add(2, "Nut", DBNull.Value);
            return table;
        }

        [TestMethod]
        public async Task Materialize_MapsColumnsToDictionaryByName()
        {
            using var reader = BuildTable().CreateDataReader();

            var rows = await RowMaterializer.MaterializeAsync(reader);

            Assert.HasCount(2, rows);
            Assert.AreEqual(1, rows[0]["Id"]);
            Assert.AreEqual("Bolt", rows[0]["Name"]);
            Assert.AreEqual(5.00m, rows[0]["Price"]);
        }

        [TestMethod]
        public async Task Materialize_MapsDbNullToNull()
        {
            using var reader = BuildTable().CreateDataReader();

            var rows = await RowMaterializer.MaterializeAsync(reader);

            Assert.AreEqual("Nut", rows[1]["Name"]);
            Assert.IsNull(rows[1]["Price"]);
        }

        [TestMethod]
        public async Task Materialize_EmptyReader_ReturnsEmpty()
        {
            var empty = new DataTable();
            empty.Columns.Add("Id", typeof(int));
            using var reader = empty.CreateDataReader();

            var rows = await RowMaterializer.MaterializeAsync(reader);

            Assert.IsEmpty(rows);
        }

        [TestMethod]
        public void MapRow_MapsCurrentRowByName()
        {
            using var reader = BuildTable().CreateDataReader();
            Assert.IsTrue(reader.Read());

            var row = RowMaterializer.MapRow(reader);

            Assert.AreEqual(1, row["Id"]);
            Assert.AreEqual("Bolt", row["Name"]);
            Assert.AreEqual(5.00m, row["Price"]);
        }

        [TestMethod]
        public void MapRow_MapsDbNullToNull()
        {
            using var reader = BuildTable().CreateDataReader();
            Assert.IsTrue(reader.Read());
            Assert.IsTrue(reader.Read()); // second row has a DBNull Price

            var row = RowMaterializer.MapRow(reader);

            Assert.AreEqual("Nut", row["Name"]);
            Assert.IsNull(row["Price"]);
        }
    }
}
