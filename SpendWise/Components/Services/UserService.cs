using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using SpendWise.Components.Models;
using static SpendWise.Components.Models.AppModel;


namespace SpendWise.Components.Services
{
    public class UserService
    {
        private readonly string _folderPath;
        private readonly string _filePath;

        public UserService()
        {
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            _folderPath = Path.Combine(desktopPath, "SpendWiseData");
            _filePath = Path.Combine(_folderPath, "Data.json");
        }

        public async Task<AppModel> LoadDataAsync()
        {
            if (!File.Exists(_filePath))
                return new AppModel();

            try
            {
                var json = await File.ReadAllTextAsync(_filePath);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                };
                return JsonSerializer.Deserialize<AppModel>(json, options) ?? new AppModel();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading data: {ex.Message}");
                return new AppModel();
            }
        }

        public async Task SaveDataAsync(AppModel systemData)
        {
            try
            {
                if (!Directory.Exists(_folderPath))
                    Directory.CreateDirectory(_folderPath);

                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(systemData, options);
                await File.WriteAllTextAsync(_filePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving data: {ex.Message}");
            }
        }

        public async Task<string> HashPasswordAsync(string password)
        {
            return await Task.Run(() =>
            {
                var salt = new byte[16];
                RandomNumberGenerator.Fill(salt); // Use RandomNumberGenerator to fill the salt

                using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 10000, HashAlgorithmName.SHA256);
                var hash = pbkdf2.GetBytes(20);

                var hashBytes = new byte[36];
                Array.Copy(salt, 0, hashBytes, 0, 16);
                Array.Copy(hash, 0, hashBytes, 16, 20);

                return Convert.ToBase64String(hashBytes);
            });
        }


        public async Task<bool> ValidatePasswordAsync(string inputPassword, string storedPassword)
        {
            return await Task.Run(() =>
            {
                try
                {
                    // Decode the stored password
                    var hashBytes = Convert.FromBase64String(storedPassword);

                    // Extract the salt
                    var salt = new byte[16];
                    Array.Copy(hashBytes, 0, salt, 0, 16);

                    // Hash the input password using the extracted salt
                    using var pbkdf2 = new Rfc2898DeriveBytes(inputPassword, salt, 10000, HashAlgorithmName.SHA256);
                    var hash = pbkdf2.GetBytes(20);

                    // Compare the result with the stored hash
                    for (int i = 0; i < 20; i++)
                    {
                        if (hashBytes[i + 16] != hash[i])
                        {
                            return false; // Mismatch
                        }
                    }

                    return true; // Password matches
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error validating password: {ex.Message}");
                    return false;
                }
            });
        }


        public async Task<bool> RegisterUserAsync(string username, string email, string password)
        {
            var systemData = await LoadDataAsync();

            if (systemData.Users.Any(u => u.UserName.Equals(username, StringComparison.OrdinalIgnoreCase)))
                return false;

            var hashedPassword = await HashPasswordAsync(password);
            systemData.Users.Add(new AppModel.UserModel
            {
                UserId = Guid.NewGuid().ToString(),
                UserName = username,
                Password = hashedPassword,
                Email = email
            });

            await SaveDataAsync(systemData);
            return true;
        }

        public async Task<bool> SaveCashInFlowDataAsync(string userId, string userName, string label, string notes, decimal amount, AppModel.TransactionType transactionType)
        {
            var systemData = await LoadDataAsync();

            systemData.Transactions.Add(new AppModel.Transaction
            {
                TransactionId = Guid.NewGuid().ToString(),
                Amount = amount,
                Label = label,
                Notes = notes,
                TransactionType = transactionType,
                TransactionDateTime = DateTime.UtcNow,
                UserId = userId,
                UserName = userName
            });

            await SaveDataAsync(systemData);
            return true;
        }

        public async Task<List<AppModel.Transaction>> GetCashInflowsAsync(string userId)
        {
            var systemData = await LoadDataAsync();
            return systemData.Transactions
                .Where(t => t.UserId == userId && t.TransactionType == AppModel.TransactionType.Credit)
                .ToList();
        }

        public async Task<decimal> GetTotalCashInflowAsync(string userId)
        {
            var systemData = await LoadDataAsync();
            return systemData.Transactions
                .Where(t => t.UserId == userId && t.TransactionType == AppModel.TransactionType.Credit)
                .Sum(t => t.Amount);
        }

        public async Task<bool> SaveCashOutFlowDataAsync(string userId, string userName, string category, string notes, decimal amount)
        {
            var systemData = await LoadDataAsync();

            systemData.Transactions.Add(new AppModel.Transaction
            {
                TransactionId = Guid.NewGuid().ToString(),
                Amount = amount,
                Label = category,
                Notes = notes,
                TransactionType = AppModel.TransactionType.Debit,
                TransactionDateTime = DateTime.UtcNow,
                IsCleared = true, // Regular cash outflow is marked cleared
                UserId = userId,
                UserName = userName
            });

            await SaveDataAsync(systemData);
            return true;
        }


        public async Task<decimal> GetTotalCashOutflowAsync(string userId)
        {
            var systemData = await LoadDataAsync();
            return systemData.Transactions
                .Where(t => t.UserId == userId && t.TransactionType == AppModel.TransactionType.Debit)
                .Sum(t => t.Amount);
        }

        public async Task<decimal> GetTotalBalanceAsync(string userId)
        {
            var systemData = await LoadDataAsync();

            // Total inflow
            var totalInflow = systemData.Transactions
                .Where(t => t.UserId == userId && t.TransactionType == TransactionType.Credit)
                .Sum(t => t.Amount);

            // Total cleared outflow
            var totalOutflow = systemData.Transactions
                .Where(t => t.UserId == userId && t.TransactionType == TransactionType.Debit && t.IsCleared)
                .Sum(t => t.Amount);

            // Total balance available (excluding debts)
            return totalInflow - totalOutflow;
        }





        public async Task<decimal> GetTotalDebtAsync(string userId)
        {
            var systemData = await LoadDataAsync();
            return systemData.Transactions
                .Where(t => t.UserId == userId && t.TransactionType == AppModel.TransactionType.Debit)
                .Sum(t => t.Amount);
        }


        public async Task<bool> ClearDebtAsync(string userId, string transactionId)
        {
            var systemData = await LoadDataAsync();

            // Find the debt transaction
            var debtTransaction = systemData.Transactions.FirstOrDefault(t =>
                t.TransactionId == transactionId && t.UserId == userId && t.TransactionType == AppModel.TransactionType.Debit && !t.IsCleared);

            if (debtTransaction == null)
            {
                Console.WriteLine("Debt not found or already cleared.");
                return false;
            }

            // Calculate available balance
            var totalInflow = systemData.Transactions
                .Where(t => t.UserId == userId && t.TransactionType == AppModel.TransactionType.Credit)
                .Sum(t => t.Amount);

            var totalOutflow = systemData.Transactions
                .Where(t => t.UserId == userId && t.TransactionType == AppModel.TransactionType.Debit && t.IsCleared)
                .Sum(t => t.Amount);

            var availableBalance = totalInflow - totalOutflow;

            if (availableBalance < debtTransaction.Amount)
            {
                Console.WriteLine("Insufficient balance to clear the debt.");
                return false;
            }

            // Deduct from balance and mark debt as cleared
            debtTransaction.IsCleared = true;

            await SaveDataAsync(systemData);
            return true;
        }

        public async Task<decimal> GetRemainingDebtAsync(string userId)
        {
            var systemData = await LoadDataAsync();
            return systemData.Transactions
                .Where(t => t.UserId == userId && t.TransactionType == AppModel.TransactionType.Debit && !t.IsCleared)
                .Sum(t => t.Amount);
        }
        public async Task<bool> CreateDebtForCashOutflowAsync(string userId, string userName, decimal debtAmount, string category, string notes)
        {
            var systemData = await LoadDataAsync();

            try
            {
                systemData.Transactions.Add(new Transaction
                {
                    TransactionId = Guid.NewGuid().ToString(),
                    Amount = debtAmount,
                    Label = category,
                    Notes = notes,
                    TransactionType = TransactionType.Debit,
                    TransactionDateTime = DateTime.UtcNow,
                    IsCleared = false,
                    DueDate = DateTime.UtcNow.AddMonths(1),
                    UserId = userId,
                    UserName = userName
                });

                await SaveDataAsync(systemData);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating debt: {ex.Message}");
                return false;
            }
        }

        public async Task<List<AppModel.Transaction>> GetCashOutflowsAsync(string userId)
        {
            var systemData = await LoadDataAsync();
            return systemData.Transactions
                .Where(t => t.UserId == userId && t.TransactionType == AppModel.TransactionType.Debit)
                .ToList();
        }
    }
}
