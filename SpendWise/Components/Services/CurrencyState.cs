using System;

namespace SpendWise.Components.Services
{
    public class CurrencyState
    {

        public string? Currency { get; set; }  // CashType can be "USD" or "NPR"

        // Event to notify users about state changes
        public event Action OnChange;

        // Method to set the username and notify any listeners
        public void SetData( string currency)
        {

            Currency = currency;
            NotifyStateChanged();
        }

        // Private method to notify about state changes
        private void NotifyStateChanged() => OnChange?.Invoke();

        // Method to convert the entered amount based on CashType
        public decimal ConvertToUSD(decimal amount)
        {
            if (Currency == "NPR")
            {

                decimal exchangeRate = 138.61m;
                return amount / exchangeRate;

            }

            return amount;
        }


        public decimal ConvertToNPR(decimal amountInUSD)
        {
            if (Currency == "USD")
            {
                // No conversion needed if it's in USD
                return amountInUSD;
            }

            // If CashType is NPR, convert USD to NPR
            decimal exchangeRate = 138.61m; // Example exchange rate: 1 USD = 133 NPR
            return amountInUSD * exchangeRate;
        }
    }
}