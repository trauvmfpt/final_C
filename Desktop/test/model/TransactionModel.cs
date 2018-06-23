using System.Collections.Generic;
using ConsoleApp3.model;
using SpringHeroBank.entity;


namespace SpringHeroBank.model
{
    public class TransactionModel
    {
        public static List<Transaction> ReadTransactions(string username)
        
        {
            var list = new List<Transaction>();
            DbConnection.Instance().OpenConnection();
            var queryString = "select * from `transactions` where username = @username";
            
            return list;
        }
    }
}