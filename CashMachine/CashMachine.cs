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

        public uint TotalStoredCash => SumOfMoneyStack(StoredCash);

        private static uint SumOfMoneyStack(IDictionary<CashNominal, uint> stack)
        {
            return stack.Aggregate(0u, (u, pair) => u + (pair.Value * (uint)pair.Key));
        }
        public CashMachine(Dictionary<CashNominal, uint> capacities)
        {
            foreach (var (nominal, capacity) in capacities)
            {
                StoredCash[nominal] = 0;
                _maxStoredCash[nominal] = capacity;
            }
        }

        private void TryUseSmallerBills(Dictionary<CashNominal, uint> transaction)
        {
            var stateAfterTransaction = new Dictionary<CashNominal, uint>(StoredCash);
            foreach (var (nominal, count) in transaction)
            {
                stateAfterTransaction[nominal] -= count;
            }

            var nominals = Enum.GetValues(typeof(CashNominal)).Cast<uint>().Reverse().ToList();
            for (int i = 0; i < nominals.Count-1; i++)
            {
                var nominalNumberValue = nominals[i];
                var nominal = (CashNominal)nominalNumberValue;
                
                    for (int j = i + 1; j < nominals.Count; j++)
                    {
                        if (transaction[nominal] == 0)
                        {
                            break;
                        }
                        
                        var lowerNominalNumberValue = nominals[j];
                        var lowerNominal = (CashNominal)lowerNominalNumberValue;
                        var countToReplace = nominalNumberValue / lowerNominalNumberValue;
                        while (transaction[nominal] > 0 &&
                               stateAfterTransaction[lowerNominal] >= nominalNumberValue / lowerNominalNumberValue &&
                               countToReplace * lowerNominalNumberValue == nominalNumberValue)
                        {
                            transaction[nominal]--;
                            transaction[lowerNominal] += countToReplace;

                            stateAfterTransaction[nominal]++;
                            stateAfterTransaction[lowerNominal] -= countToReplace;
                        }
                    }
            }
        }
        public (bool, Dictionary<CashNominal, uint>?) Withdraw(uint requestedAmount, bool useSmallerBills = false)
        {
            if (requestedAmount == 0) { return (false, null);}
            var transaction = new Dictionary<CashNominal, uint>();
            var currentCopyOfAmountOfMoney = requestedAmount;
            foreach (var (nominal,count) in StoredCash.Reverse())
            {
                var needToAdd = (uint)Math.Floor((double)currentCopyOfAmountOfMoney / (uint)nominal);
                var possibleToAdd = Math.Min(needToAdd, count);
                transaction[nominal] = possibleToAdd;
                currentCopyOfAmountOfMoney -= possibleToAdd * (uint)nominal;
            }

            //check transaction total sum:
            if (SumOfMoneyStack(transaction) != requestedAmount)
            {
                return (false, null);
            }

            //We verified that it is possible to create transaction at all. Now try to find same transaction but with smaller bills.
            if (useSmallerBills)
            {
                TryUseSmallerBills(transaction);
            }

            //apply transaction at full
            foreach (var (nominal, count) in transaction)
            {
                StoredCash[nominal] -= count;
            }
            //transaction was successful
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
    }
}