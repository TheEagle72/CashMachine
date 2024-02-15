namespace CashMachines
{
    public enum CashNominal
    {
        _10 = 10,
        _50 = 50,
        _100 = 100,
        _500 = 500,
        _1000 = 1000,
        _2000 = 2000,
        _5000 = 5000
    }
    public interface ICashInfo
    {
        IDictionary<CashNominal, uint> StoredCash { get; }
        public uint TotalStoredCash { get; }
    }

    public interface ICashDeposit : ICashInfo
    {
        bool Deposit(CashNominal nominal, uint count);
        bool DepositStackOfMoney(IDictionary<CashNominal, uint> stackOfMoney);
    }

    public interface ICashWithdrawal : ICashInfo
    {
        (bool, Dictionary<CashNominal, uint>?) Withdraw(uint requestedAmount, bool useSmallerBills = false);
    }

    interface ICashDepositAndWithdrawal : ICashDeposit, ICashWithdrawal;

    public class CashMachine : ICashDepositAndWithdrawal
    {
        public IDictionary<CashNominal, uint> StoredCash { get;  } = new SortedDictionary<CashNominal, uint>();

        private readonly IDictionary<CashNominal, uint> _maxStoredCash = new Dictionary<CashNominal, uint>(); // read only  property not working either :(

        public uint TotalStoredCash => TransactionTotal(StoredCash);
        public CashMachine(Dictionary<CashNominal, uint> capacities)
        {
            foreach (var (nominal, capacity) in capacities)
            {
                StoredCash[nominal] = 0;
                _maxStoredCash[nominal] = capacity;
            }
        }

        public (bool, Dictionary<CashNominal, uint>?) Withdraw(uint requestedAmount, bool useSmallerBills = false)
        {
            if (requestedAmount == 0) { return (false, null); }

            //Dynamic programming. Knapsack problem
            var sums = new Dictionary<uint, uint> { { 0, 0 } };
            
            var newSums = new Dictionary<uint, uint>();
            var solutionFound = false;
            foreach (var (nominal, count) in StoredCash)
            {
                if (solutionFound)
                {
                    break;
                }

                var value = (uint)nominal;
                for (var i = 0; i < count; i++)
                {
                    foreach (var sum in sums.Keys)
                    {
                        var newSum = sum + value;

                        if (newSum > requestedAmount)
                        {
                            continue;
                        }

                        if (!sums.ContainsKey(newSum))
                        {
                            newSums[newSum] = value;
                        }
                    }

                    foreach (var (key, val) in newSums)
                    {
                        sums.Add(key, val);
                    }
                    newSums.Clear();

                    if (sums.ContainsKey(requestedAmount))
                    {
                        solutionFound = true;
                        break;
                    }
                }
            }

            if (!sums.ContainsKey(requestedAmount))
            {
                return (false, null);
            }

            var transaction = new Dictionary<CashNominal, uint>();
            var remainingAmountToWithdraw = requestedAmount;

            while (remainingAmountToWithdraw > 0)
            {
                var nominal = (CashNominal)sums[remainingAmountToWithdraw];
                
                transaction.TryAdd(nominal, 0);
                
                ++transaction[nominal];
                --StoredCash[nominal];

                remainingAmountToWithdraw -= (uint)nominal;
            }

            return (true, transaction);
        }

        public uint CurrentlyStoredAmountOfSpecificNominal(CashNominal nominal)
        {
            return StoredCash[nominal];
        }
        public uint MaxCapacityOfSpecificNominal(CashNominal nominal)
        {
            return _maxStoredCash[nominal];
        }

        public uint RemainingCapacityOfSpecificNominal(CashNominal nominal)
        {
            return MaxCapacityOfSpecificNominal(nominal) - CurrentlyStoredAmountOfSpecificNominal(nominal);
        }

        public bool Deposit(CashNominal nominal, uint count)
        {
            if (!Enum.IsDefined(nominal)) { return false; }
            if (!StoredCash.ContainsKey(nominal)) { return false; }
            if (count > RemainingCapacityOfSpecificNominal(nominal)) { return false; }
            if (count == 0) { return false; }

            StoredCash[nominal] += count;
            return true;
        }

        public bool DepositStackOfMoney(IDictionary<CashNominal, uint> stackOfMoney)
        {
            if (stackOfMoney.Count == 0) { return false; }
            foreach (var (nominal, count) in stackOfMoney)
            {
                if (count > RemainingCapacityOfSpecificNominal(nominal))
                {
                    return false;
                }
            }
            
            foreach (var (nominal, count) in stackOfMoney)
            {
                Deposit(nominal, count);
            }
            
            return true;
        }

        public static uint TransactionTotal(IDictionary<CashNominal, uint> transaction)
        {
            return transaction.Aggregate(0u, (u, nominalCount) => u + (uint)nominalCount.Key * nominalCount.Value);
        }
    }
} 