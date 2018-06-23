using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using SpringHeroBank.entity;
using DbConnection = ConsoleApp3.model.DbConnection;

namespace SpringHeroBank.model
{
    public class TransactionsModel
    {
        public static List<Transaction> ReadTransactions()
        {
            var list = new List<Transaction>();
            DbConnection.Instance().OpenConnection();
            var queryString = "select * from `transactions` where username = @username";
            
            return list;
        }
    }
}