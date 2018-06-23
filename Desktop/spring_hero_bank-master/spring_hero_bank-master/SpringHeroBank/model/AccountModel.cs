using System;
using System.Globalization;
using System.IO.Pipes;
using System.Reflection.PortableExecutable;
using ConsoleApp3.model;
using MySql.Data.MySqlClient;
using SpringHeroBank.entity;
using SpringHeroBank.error;
using SpringHeroBank.utility;

namespace SpringHeroBank.model
{
    public class AccountModel
    {
        public Boolean Save(Account account)
        {
            DbConnection.Instance().OpenConnection(); // đảm bảo rằng đã kết nối đến db thành công.
            var salt = Hash.RandomString(7); // sinh ra chuỗi muối random.
            account.Salt = salt; // đưa muối vào thuộc tính của account để lưu vào database.
            // mã hoá password của người dùng kèm theo muối, set thuộc tính password mới.
            account.Password = Hash.GenerateSaltedSHA1(account.Password, account.Salt);
            var sqlQuery = "insert into `accounts` " +
                           "(`username`, `password`, `accountNumber`, `identityCard`, `balance`, `phone`, `email`, `fullName`, `salt`, `status`) values" +
                           "(@username, @password, @accountNumber, @identityCard, @balance, @phone, @email, @fullName, @salt, @status)";
            var cmd = new MySqlCommand(sqlQuery, DbConnection.Instance().Connection);
            cmd.Parameters.AddWithValue("@username", account.Username);
            cmd.Parameters.AddWithValue("@password", account.Password);
            cmd.Parameters.AddWithValue("@accountNumber", account.AccountNumber);
            cmd.Parameters.AddWithValue("@identityCard", account.IdentityCard);
            cmd.Parameters.AddWithValue("@balance", account.Balance);
            cmd.Parameters.AddWithValue("@phone", account.Phone);
            cmd.Parameters.AddWithValue("@email", account.Email);
            cmd.Parameters.AddWithValue("@fullName", account.FullName);
            cmd.Parameters.AddWithValue("@salt", account.Salt);
            cmd.Parameters.AddWithValue("@status", account.Status);
            var result = cmd.ExecuteNonQuery();
            DbConnection.Instance().CloseConnection();
            return result == 1;
        }

        public bool CheckEnoughBalance(decimal amount)
        {
            Program.currentLoggedIn = GetAccountByUserName(Program.currentLoggedIn.Username);
            if(amount > Program.currentLoggedIn.Balance)
            {
                return false;
            }
            return true;
        }

        public bool Transfer(Account sender, Account receiver, Transaction historyTransaction)
        {
            /*DbConnection.Instance().OpenConnection();
            var transaction = DbConnection.Instance().Connection.BeginTransaction();*/
            var updateSenderBalance = UpdateBalance(sender, historyTransaction);
            if (!updateSenderBalance)
            {
                Console.WriteLine("Can't update sender's balance.");
                return false;
            }
            var updateReceiverBalance = UpdateBalance(receiver, historyTransaction);
            if (!updateReceiverBalance)
            {
                Console.WriteLine("Can't update receiver's balance.");
                return DeleteTransaction(historyTransaction);
            }
            var updateTransactionResult = UpdateTransactionStatus(historyTransaction.Id, 2);
            if (!updateTransactionResult)
            {
                Console.WriteLine("Can't update transaction's status.");
                return DeleteTransaction(historyTransaction);
            }
            if (updateSenderBalance && updateReceiverBalance && updateTransactionResult)
            {
                /*transaction.Commit();
                DbConnection.Instance().CloseConnection();*/
                return true;
            }
            /*transaction.Rollback();
            DbConnection.Instance().CloseConnection();*/
            //Nested transactions are not supported.
            return false;
        }

        public bool DeleteTransaction(Transaction historyTransaction)
        {
            DbConnection.Instance().OpenConnection();
            var queryDeleteTransaction = "delete from `transactions` where id = @id";
            var cmdUpdateTransaction =
                new MySqlCommand(queryDeleteTransaction, DbConnection.Instance().Connection);
            cmdUpdateTransaction.Parameters.AddWithValue("@id", historyTransaction.Id);
            var deleteTransactionResult = cmdUpdateTransaction.ExecuteNonQuery();
            if (deleteTransactionResult == 1)
            {
                return false;
            }
            return true;
        }

        public bool UpdateTransactionStatus(string id, int status)
        {
            var updateTransactionResult = 0;
            var queryUpdateTransaction = "update `transactions` set status = @status where id = @id";
            var cmdUpdateTransaction =
                new MySqlCommand(queryUpdateTransaction, DbConnection.Instance().Connection);
            cmdUpdateTransaction.Parameters.AddWithValue("@id", id);
            cmdUpdateTransaction.Parameters.AddWithValue("@status", status);
            updateTransactionResult = cmdUpdateTransaction.ExecuteNonQuery();
            if (updateTransactionResult == 1)
            {
                return true;
            }
            return false;
        }

        public bool UpdateBalance(Account account, Transaction historyTransaction)
        {
            DbConnection.Instance().OpenConnection(); // đảm bảo rằng đã kết nối đến db thành công.
            var trans = DbConnection.Instance().Connection.BeginTransaction(); // Khởi tạo transaction.

            try
            {
                /**
                 * 1. Lấy thông tin số dư mới nhất của tài khoản.
                 * 2. Kiểm tra kiểu transaction. Chỉ chấp nhận deposit và withdraw.
                 *     2.1. Kiểm tra số tiền rút nếu kiểu transaction là withdraw.                 
                 * 3. Update số dư vào tài khoản.
                 *     3.1. Tính toán lại số tiền trong tài khoản.
                 *     3.2. Update số tiền vào database.
                 * 4. Lưu thông tin transaction vào bảng transaction.
                 */

                // 1. Lấy thông tin số dư mới nhất của tài khoản.
                var queryBalance = "select balance from `accounts` where username = @username and status = @status";
                MySqlCommand queryBalanceCommand = new MySqlCommand(queryBalance, DbConnection.Instance().Connection);
                queryBalanceCommand.Parameters.AddWithValue("@username", account.Username);
                queryBalanceCommand.Parameters.AddWithValue("@status", account.Status);
                var balanceReader = queryBalanceCommand.ExecuteReader();
                // Không tìm thấy tài khoản tương ứng, throw lỗi.
                if (!balanceReader.Read())
                {
                    // Không tồn tại bản ghi tương ứng, lập tức rollback transaction, trả về false.
                    // Hàm dừng tại đây.
                    //Console.WriteLine("Invalid username");
                    throw new SpringHeroTransactionException("Account is disabled or doesn't exist.");
                }

                // Đảm bảo sẽ có bản ghi.
                var currentBalance = balanceReader.GetDecimal("balance");
                balanceReader.Close();

                // 2. Kiểm tra kiểu transaction. Chỉ chấp nhận deposit và withdraw. 
                if (historyTransaction.Type != Transaction.TransactionType.DEPOSIT
                    && historyTransaction.Type != Transaction.TransactionType.WITHDRAW
                    && historyTransaction.Type != Transaction.TransactionType.TRANSFER)
                {
                    throw new SpringHeroTransactionException("Invalid transaction type!");
                }

                // 2.1. Kiểm tra số tiền rút nếu kiểu transaction là withdraw.
                if (historyTransaction.Type == Transaction.TransactionType.WITHDRAW &&
                    !CheckEnoughBalance(historyTransaction.Amount))
                {
                    throw new SpringHeroTransactionException("Not enough money to make transaction.");
                }

                // 3. Update số dư vào tài khoản.
                // 3.1. Tính toán lại số tiền trong tài khoản.
                if (historyTransaction.Type == Transaction.TransactionType.DEPOSIT || 
                    (historyTransaction.Type == Transaction.TransactionType.TRANSFER && historyTransaction.ReceiverAccountNumber == account.AccountNumber))
                {
                    currentBalance += historyTransaction.Amount;
                }
                else
                {
                    currentBalance -= historyTransaction.Amount;
                }

                // 3.2. Update số dư vào database.
                var updateAccountResult = 0;
                var queryUpdateAccountBalance =
                    "update `accounts` set balance = @balance where username = @username and status = 1";
                
                var cmdUpdateAccountBalance =
                    new MySqlCommand(queryUpdateAccountBalance, DbConnection.Instance().Connection);
                cmdUpdateAccountBalance.Parameters.AddWithValue("@username", account.Username);
                cmdUpdateAccountBalance.Parameters.AddWithValue("@balance", currentBalance);
                updateAccountResult = cmdUpdateAccountBalance.ExecuteNonQuery();
                
                // 4. Lưu thông tin transaction vào bảng transaction.
                if (historyTransaction.Type == Transaction.TransactionType.TRANSFER && historyTransaction.ReceiverAccountNumber == account.AccountNumber)
                {
                    var updateTransactionResult = 0;
                    var queryUpdateTransaction = "update `transactions` set amount = @amount where id = @id";
                    var cmdUpdateTransaction =
                        new MySqlCommand(queryUpdateTransaction, DbConnection.Instance().Connection);
                    cmdUpdateTransaction.Parameters.AddWithValue("@id", historyTransaction.Id);
                    cmdUpdateTransaction.Parameters.AddWithValue("@amount", historyTransaction.Amount);
                    updateTransactionResult = cmdUpdateTransaction.ExecuteNonQuery();
                    if (updateAccountResult == 1 && updateTransactionResult == 1)
                    {
                        trans.Commit();
                        return true;
                    }
                }
                else
                {
                    var insertTransactionResult = 0;
                    var queryInsertTransaction = "insert into `transactions` " +
                                                 "(id, transactionType, amount, content, senderAccountNumber, receiverAccountNumber, status) " +
                                                 "values (@id, @type, @amount, @content, @senderAccountNumber, @receiverAccountNumber, @status)";
                    var cmdInsertTransaction =
                        new MySqlCommand(queryInsertTransaction, DbConnection.Instance().Connection);
                    cmdInsertTransaction.Parameters.AddWithValue("@id", historyTransaction.Id);
                    cmdInsertTransaction.Parameters.AddWithValue("@type", historyTransaction.Type);
                    cmdInsertTransaction.Parameters.AddWithValue("@amount", historyTransaction.Amount);
                    cmdInsertTransaction.Parameters.AddWithValue("@content", historyTransaction.Content);
                    cmdInsertTransaction.Parameters.AddWithValue("@senderAccountNumber",
                        historyTransaction.SenderAccountNumber);
                    cmdInsertTransaction.Parameters.AddWithValue("@receiverAccountNumber",
                        historyTransaction.ReceiverAccountNumber);
                    cmdInsertTransaction.Parameters.AddWithValue("@status", historyTransaction.Status);
                    insertTransactionResult = cmdInsertTransaction.ExecuteNonQuery();
                    if (updateAccountResult == 1 && insertTransactionResult == 1)
                    {
                        trans.Commit();
                        DbConnection.Instance().CloseConnection();
                        return true;
                    }
                }
            }
            catch (SpringHeroTransactionException e)
            {
                Console.WriteLine(e.Message);
                trans.Rollback();
                DbConnection.Instance().CloseConnection();
                return false;
            }

            DbConnection.Instance().CloseConnection();
            return false;
        }

        public Boolean CheckExistAccountNumber(string accountNumber)
        {    
            DbConnection.Instance().OpenConnection();
            var queryString = "select * from  `accounts` where accountNumber = @accountNumber and status = 1";
            var cmd = new MySqlCommand(queryString, DbConnection.Instance().Connection);
            cmd.Parameters.AddWithValue("@accountNumber", accountNumber);
            var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                reader.Close();
                return true;
            }
            reader.Close();
            DbConnection.Instance().CloseConnection();
            return false;
        }

        public Account GetAccountByUserName(string username)
        {
            DbConnection.Instance().OpenConnection();
            var queryString = "select * from  `accounts` where username = @username and status = 1";
            var cmd = new MySqlCommand(queryString, DbConnection.Instance().Connection);
            cmd.Parameters.AddWithValue("@username", username);
            var reader = cmd.ExecuteReader();
            Account account = null;
            if (reader.Read())
            {
                var _username = reader.GetString("username");
                var password = reader.GetString("password");
                var salt = reader.GetString("salt");
                var accountNumber = reader.GetString("accountNumber");
                var identityCard = reader.GetString("identityCard");
                var balance = reader.GetDecimal("balance");
                var phone = reader.GetString("phone");
                var email = reader.GetString("email");
                var fullName = reader.GetString("fullName");
                var createdAt = reader.GetString("createdAt");
                var updatedAt = reader.GetString("updatedAt");
                var status = reader.GetInt32("status");
                account = new Account(_username, password, salt, accountNumber, identityCard, balance, phone, email,
                    fullName, createdAt, updatedAt, (Account.ActiveStatus) status);
            }
            reader.Close();
            DbConnection.Instance().CloseConnection();
            return account;
        }
        
        public Account GetAccountByAccountNumber(string accountNumber)
        {
            DbConnection.Instance().OpenConnection();
            var queryString = "select * from  `accounts` where accountNumber = @accountNumber and status = 1";
            var cmd = new MySqlCommand(queryString, DbConnection.Instance().Connection);
            cmd.Parameters.AddWithValue("@accountNumber", accountNumber);
            var reader = cmd.ExecuteReader();
            Account account = null;
            if (reader.Read())
            {
                var username = reader.GetString("username");
                var password = reader.GetString("password");
                var salt = reader.GetString("salt");
                var _accountNumber = reader.GetString("accountNumber");
                var identityCard = reader.GetString("identityCard");
                var balance = reader.GetDecimal("balance");
                var phone = reader.GetString("phone");
                var email = reader.GetString("email");
                var fullName = reader.GetString("fullName");
                var createdAt = reader.GetString("createdAt");
                var updatedAt = reader.GetString("updatedAt");
                var status = reader.GetInt32("status");
                account = new Account(username, password, salt, _accountNumber, identityCard, balance, phone, email,
                    fullName, createdAt, updatedAt, (Account.ActiveStatus) status);
            }
            reader.Close();
            DbConnection.Instance().CloseConnection();
            return account;
        }
        
        public bool ShowTransactionHistoryByDate()
        {
            return false;
        }

        public bool ShowTransactionHistoryByTimePeriod()
        {
            return false;
        }
    }
}