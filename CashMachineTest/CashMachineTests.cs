using CashMachines;
using System.Data.Common;
using System.Diagnostics.Metrics;
using System.Transactions;

namespace CashMachineTest
{
    public class Tests
    {
        private static uint smallIterCount = 10u;
        private static uint midIterCount = 100u;
        private static uint bigIterCount = 1000u;

        private CashNominal[] GetAllPossibleNominals()
        {
            return Enum.GetValues(typeof(CashNominal)).Cast<CashNominal>().ToArray();
        }
        private Dictionary<CashNominal, uint> FillSameCapacities(uint capacity)
        {
            return GetAllPossibleNominals().ToDictionary(nominal => nominal, nominal => capacity);
        }


        [Test]
        public void EmptyCashMachine()
        {
            var cashMachine = new CashMachine(new Dictionary<CashNominal, uint>());

            Assert.IsNotNull(cashMachine);
            Assert.True(cashMachine.StoredCash.Count == 0);
            
            Assert.AreEqual(cashMachine.TotalStoredCash, 0);
            //try insert value, which is not in accepted in this Cash Machine in this case - any
            Assert.False(cashMachine.Deposit(CashNominal._10, 1));
            Assert.False(cashMachine.Deposit(CashNominal._1000, 1));

            var (result, transaction) = cashMachine.Withdraw(100);
            Assert.False(result);
            Assert.IsNull(transaction);

            Assert.AreEqual(cashMachine.TotalStoredCash, 0);
        }

        [TestCase(0u)]
        [TestCase(10u)]
        [TestCase(100u)]
        [TestCase(1000u)]
        public void ConstructionCorrectMaxCapacity(uint capacity)
        {
            var cashMachine = new CashMachine(FillSameCapacities(capacity));
            Assert.IsNotNull(cashMachine);

            Assert.AreEqual(0, cashMachine.TotalStoredCash);
            foreach (CashNominal nominal in GetAllPossibleNominals())
            {
                Assert.AreEqual(capacity, cashMachine.MaxCapacityOfSpecificNominal(nominal));
            }
            Assert.AreEqual(0, cashMachine.TotalStoredCash);
            Assert.True(cashMachine.StoredCash.Values.All(val  => val == 0));
        }

        [Test]
        public void DepositZero()
        {
            var cashMachine = new CashMachine(FillSameCapacities(5000));
            Assert.AreEqual(0, cashMachine.TotalStoredCash);
            Assert.IsNotNull(cashMachine);
            for (int i = 0; i < 100; i++)
            {
                Assert.False(cashMachine.Deposit(CashNominal._100, 0u));
                Assert.AreEqual(0, cashMachine.StoredCash[CashNominal._100]);
                Assert.AreEqual(0, cashMachine.TotalStoredCash);
            }
        }

        [TestCase(10u, CashNominal._10)]
        [TestCase(20u, CashNominal._10)]
        [TestCase(30u, CashNominal._10)]
        [TestCase(50u, CashNominal._10)]
        
        [TestCase(10u, CashNominal._100)]
        [TestCase(20u, CashNominal._100)]
        [TestCase(30u, CashNominal._100)]
        [TestCase(50u, CashNominal._100)]
        public void DepositValid(uint cashAmount, CashNominal nominal)
        {
            var cashMachine = new CashMachine(FillSameCapacities(5000));
            Assert.IsNotNull(cashMachine);
            Assert.AreEqual(cashMachine.TotalStoredCash, 0);
            Assert.True(cashMachine.StoredCash.ContainsKey(nominal));
            for (int i = 0; i < 100; i++)
            {
                Assert.True(cashMachine.Deposit(nominal, cashAmount));
                Assert.AreEqual(cashAmount * (i + 1), cashMachine.StoredCash[nominal]);
                Assert.AreEqual(cashAmount * (uint)nominal * (i + 1), cashMachine.TotalStoredCash );
            }
        }

        [TestCase(10u)]
        [TestCase(20u)]
        [TestCase(30u)]
        [TestCase(40u)]
        [TestCase(50u)]
        public void DepositValidOverflow(uint cashAmount)
        {
            var nominal = CashNominal._100;
            var repeatsBeforeOverflow = 10u;
            var cashMachine = new CashMachine(FillSameCapacities(cashAmount* repeatsBeforeOverflow));
            Assert.IsNotNull(cashMachine);
            Assert.AreEqual(cashMachine.TotalStoredCash, 0);
            Assert.True(cashMachine.StoredCash.ContainsKey(nominal));
            for (int i = 0; i < repeatsBeforeOverflow; i++)
            {
                Assert.True(cashMachine.Deposit(nominal, cashAmount));
                Assert.AreEqual(cashMachine.StoredCash[nominal], cashAmount * (i + 1));
                Assert.AreEqual(cashAmount * (uint)nominal * (i + 1), cashMachine.TotalStoredCash);
            }

            for (int i = 0; i < 10; i++)
            {
                Assert.False(cashMachine.Deposit(nominal, cashAmount));
                Assert.AreEqual(cashAmount * repeatsBeforeOverflow, cashMachine.StoredCash[nominal] );
                Assert.AreEqual(cashAmount * (uint)nominal * repeatsBeforeOverflow, cashMachine.TotalStoredCash);
            }
        }

        [Test]
        public void DepositStackZero()
        {
            var cashMachine = new CashMachine(FillSameCapacities(5000));
            Assert.IsNotNull(cashMachine);
            Assert.False(cashMachine.Deposit(CashNominal._100, 0u));
            for (int i = 0; i < midIterCount; i++)
            {
                Assert.False(cashMachine.DepositStackOfMoney(new Dictionary<CashNominal, uint>()));
                Assert.AreEqual(0, cashMachine.StoredCash[CashNominal._100]);
                Assert.AreEqual(0, cashMachine.TotalStoredCash);
            }

        }

        [TestCase(0u)]
        [TestCase(1u)]
        [TestCase(10u)]
        [TestCase(20u)]
        [TestCase(30u)]
        [TestCase(50u)]
        [TestCase(100u)]
        public void DepositStackValid(uint cashAmount)
        {
            var cashMachine = new CashMachine(FillSameCapacities(100000));
            
            var nominal1 = CashNominal._100;
            var nominal2 = CashNominal._500;
            
            var count1 = cashAmount;
            var count2 = 10*cashAmount;

            Assert.IsNotNull(cashMachine);
            Assert.AreEqual(cashMachine.TotalStoredCash, 0);
            Assert.True(cashMachine.StoredCash.ContainsKey(nominal1));
            Assert.True(cashMachine.StoredCash.ContainsKey(nominal2));
            
            var transaction = new Dictionary<CashNominal, uint> { { nominal1, count1 }, { nominal2, count2 } };
            for (int i = 0; i < 100; i++)
            {
                Assert.True(cashMachine.DepositStackOfMoney(transaction));
                Assert.AreEqual(cashMachine.StoredCash[nominal1], count1 * (i + 1));
                Assert.AreEqual(cashMachine.StoredCash[nominal2], count2 * (i + 1));
                Assert.AreEqual((uint)nominal1 * (i + 1) * count1 + (uint)nominal2 * (i + 1) * count2, cashMachine.TotalStoredCash);
            }
        }

        [TestCase(10u)]
        [TestCase(20u)]
        [TestCase(30u)]
        [TestCase(40u)]
        [TestCase(50u)]
        public void DepositStackValidOverflow(uint cashAmount)
        {
            var nominal = CashNominal._100;
            var repeatsBeforeOverflow = 10u;

            var nominal1 = CashNominal._100;
            var nominal2 = CashNominal._500;

            var count1 = cashAmount;
            var count2 = 10 * cashAmount;

            var cashMachine = new CashMachine(FillSameCapacities(cashAmount * repeatsBeforeOverflow*10));

            Assert.IsNotNull(cashMachine);
            Assert.AreEqual(cashMachine.TotalStoredCash, 0);
            Assert.True(cashMachine.StoredCash.ContainsKey(nominal1));
            Assert.True(cashMachine.StoredCash.ContainsKey(nominal2));
            var transaction = new Dictionary<CashNominal, uint> { { nominal1, count1 }, { nominal2, count2 } };
            for (int i = 0; i < repeatsBeforeOverflow; i++)
            {
                Assert.True(cashMachine.DepositStackOfMoney(transaction));
                Assert.AreEqual(count1 * (i + 1), cashMachine.StoredCash[nominal1]);
                Assert.AreEqual( count2 * (i + 1), cashMachine.StoredCash[nominal2]);
                Assert.AreEqual((uint)nominal1 * (i + 1) * count1 + (uint)nominal2 * (i + 1) * count2, cashMachine.TotalStoredCash);
            }

            for (int i = 0; i < smallIterCount; i++)
            {
                Assert.False(cashMachine.DepositStackOfMoney(transaction));
                Assert.AreEqual(count1 * repeatsBeforeOverflow, cashMachine.StoredCash[nominal1]);
                Assert.AreEqual(count2 * repeatsBeforeOverflow, cashMachine.StoredCash[nominal2]);
                Assert.AreEqual((uint)nominal1 * repeatsBeforeOverflow * count1 + (uint)nominal2 * repeatsBeforeOverflow * count2, cashMachine.TotalStoredCash);
            }
        }
        
        [Test]
        public void WithdrawZeroFromEmpty()
        {
            var nominal = CashNominal._100;
            var cashMachine = new CashMachine(FillSameCapacities(1000u));

            for (int i = 0; i < smallIterCount; i++)
            {
                var (result, transaction) = cashMachine.Withdraw(0);
                Assert.False(result);
                Assert.IsNull(transaction);
            }
        }
        
        [TestCase]
        public void WithdrawZeroFromFilled()
        {
            var nominal = CashNominal._100;
            var cashMachine = new CashMachine(FillSameCapacities(1000u));

            Assert.True(cashMachine.Deposit(nominal, smallIterCount));

            for (int i = 0; i < smallIterCount; i++)
            {
                var (result, transaction) = cashMachine.Withdraw(0);
                Assert.False(result);
                Assert.IsNull(transaction);
            }
        }

        [TestCase]
        public void WithdrawMoreThanCapacity()
        {
            var cashMachine = new CashMachine(FillSameCapacities(100u));

            for (int i = 0; i < smallIterCount; i++)
            {
                var (result, transaction) = cashMachine.Withdraw((uint)(200*i));
                Assert.False(result);
                Assert.IsNull(transaction);
            }
        }
        
        [TestCase(1u)]
        [TestCase(10u)]
        [TestCase(20u)]
        [TestCase(100u)]
        [TestCase(1000u)]
        public void WithdrawValidAll(uint cashAmount)
        {
            var nominal = CashNominal._1000;

            var moreCash = cashAmount * smallIterCount;
            var cashMachine = new CashMachine(FillSameCapacities(moreCash));
            Assert.IsNotNull(cashMachine);
            Assert.AreEqual(0, cashMachine.TotalStoredCash);
            Assert.True(cashMachine.Deposit(nominal, moreCash));

            Assert.AreEqual(moreCash, cashMachine.StoredCash[nominal]);
            Assert.AreEqual(moreCash * (uint)nominal, cashMachine.TotalStoredCash);

            for (int i = 0; i < smallIterCount; i++)
            {
                var (result, transaction) = cashMachine.Withdraw((uint)nominal*cashAmount);
                Assert.True(result);
                Assert.IsNotNull(transaction);
                Assert.AreEqual(cashAmount*(smallIterCount- (i+1)), cashMachine.StoredCash[nominal]);
                Assert.AreEqual(cashAmount * (smallIterCount - (i+1))*(uint)nominal, cashMachine.TotalStoredCash);
            }

            Assert.AreEqual(0, cashMachine.StoredCash[nominal]);
            Assert.AreEqual(0, cashMachine.TotalStoredCash);

            var (result2, transaction2) = cashMachine.Withdraw((uint)nominal * cashAmount);
            Assert.False(result2);
            Assert.IsNull(transaction2);
        }
        
        [TestCase(1u)]
        [TestCase(10u)]
        [TestCase(20u)]
        [TestCase(100u)]
        [TestCase(1000u)]
        public void WithdrawValidAllSmallBills(uint cashAmount)
        {
            var nominal = CashNominal._1000;

            var moreCash = cashAmount * smallIterCount;
            var cashMachine = new CashMachine(FillSameCapacities(moreCash));
            Assert.IsNotNull(cashMachine);
            Assert.AreEqual(0, cashMachine.TotalStoredCash);
            Assert.True(cashMachine.Deposit(nominal, moreCash));

            Assert.AreEqual(moreCash, cashMachine.StoredCash[nominal]);
            Assert.AreEqual(moreCash * (uint)nominal, cashMachine.TotalStoredCash);

            for (int i = 0; i < smallIterCount; i++)
            {
                var (result, transaction) = cashMachine.Withdraw((uint)nominal*cashAmount, useSmallerBills: true);
                Assert.True(result);
                Assert.IsNotNull(transaction);
                Assert.AreEqual(cashAmount*(smallIterCount-i-1), cashMachine.StoredCash[nominal]);
                Assert.AreEqual(cashAmount * (smallIterCount - i-1)*(uint)nominal, cashMachine.TotalStoredCash);
            }
        }
        
        [TestCase(1u)]
        [TestCase(10u)]
        [TestCase(20u)]
        [TestCase(100u)]
        [TestCase(1000u)]
        public void WithdrawValidSmallBills(uint cashAmount)
        {
            var nominal1 = CashNominal._5000;
            var nominal2 = CashNominal._1000;
            var nominal3 = CashNominal._500;

            var cashMachine1 = new CashMachine(FillSameCapacities(10000u));
            var cashMachine2 = new CashMachine(FillSameCapacities(10000u));

            Assert.IsNotNull(cashMachine1);
            Assert.IsNotNull(cashMachine2);
            
            Assert.AreEqual(0, cashMachine1.TotalStoredCash);
            Assert.AreEqual(0, cashMachine2.TotalStoredCash);

            var stackOfMoney = new Dictionary<CashNominal, uint>
            {
                { nominal1, cashAmount*10 },
                { nominal2, cashAmount*10 },
                { nominal3, cashAmount*10 },
            };

            Assert.True(cashMachine1.DepositStackOfMoney(stackOfMoney));
            Assert.True(cashMachine2.DepositStackOfMoney(stackOfMoney));

            Assert.AreEqual(cashMachine1.TotalStoredCash, cashMachine2.TotalStoredCash);
            var requiredAmount = (uint)nominal1;
            var (result1, transaction1) = cashMachine1.Withdraw((uint)nominal1);
            Assert.True(result1);
            Assert.IsNotNull(transaction1);

            var (result2, transaction2) = cashMachine2.Withdraw((uint)nominal1, useSmallerBills: true);
            Assert.True(result2);
            Assert.IsNotNull(transaction2);
            Assert.AreEqual(cashMachine1.TotalStoredCash, cashMachine2.TotalStoredCash);
        }

        [TestCase(8000u, 5000u, 1u, 2000u, 4u)]
        public void WithdrawValidNotCompletelyDivisible(uint requiredAmount, uint nominal1, uint count1, uint nominal2, uint count2)
        {
            var cashMachine = new CashMachine(FillSameCapacities(1000u));

            var stackOfMoney = new Dictionary<CashNominal, uint>
            {
                { (CashNominal)nominal1, count1 },
                { (CashNominal)nominal2, count2 },
            };

            Assert.True(cashMachine.DepositStackOfMoney(stackOfMoney));

            var (result1, transaction) = cashMachine.Withdraw(requiredAmount);

            
            

            Assert.True(result1);
            Assert.IsNotNull(transaction);

            Assert.AreEqual(requiredAmount, CashMachine.TransactionTotal(transaction));

            Assert.LessOrEqual(transaction.ContainsKey((CashNominal)nominal1) ? transaction[(CashNominal)nominal1] : 0u, count1);
            Assert.LessOrEqual(transaction.ContainsKey((CashNominal)nominal2) ? transaction[(CashNominal)nominal2] : 0u, count2);
        }
    }
}