using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using CashMachines;

namespace CashMachineUi
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    ///
    internal enum CashMachineUiOperation
    {
        UiSelect,
        UiWithdraw,
        UiDeposit
    }
    
    public partial class MainWindow : Window
    {
        private const uint DefaultCashMachineCapacity = 50;

        private readonly CashMachine _cashMachine;
        
        private CashMachineUiOperation _uiOperation;

        public ObservableCollection<TupleNominalAmountCapacity> CashStoredObserver { get; } = new();
        public ObservableCollection<PairNominalAmount> CashDepositObserver { get; set; } = new();

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            

            var capacities = Enum.GetValues(typeof(CashNominal)).Cast<CashNominal>().ToDictionary(nominal => nominal, nominal => DefaultCashMachineCapacity);
            _cashMachine = new CashMachine(capacities);
            UpdateCashStoredUi();
        }

        private void UpdateCashStoredUi()
        {
            
            CashStoredObserver.Clear();
            
            foreach (var (nominal, amount) in _cashMachine.StoredCash)
            {
                CashStoredObserver.Add(new TupleNominalAmountCapacity((uint)nominal, amount, _cashMachine.MaxCapacityOfSpecificNominal(nominal)));
            }

            UiLabel_BalanceTotal.Text = $"Суммарный баланс банкомата: {_cashMachine.TotalStoredCash}"; //can be binded directly
        }

        private void UiChangeOperation(CashMachineUiOperation operation)
        {
            _uiOperation = operation;
            UiGrid_Select.Visibility = Visibility.Hidden;
            UiGrid_Deposit.Visibility = Visibility.Hidden;
            UiGrid_Withdraw.Visibility = Visibility.Hidden;

            switch (_uiOperation)
            {
                case CashMachineUiOperation.UiSelect:
                    UiGrid_Select.Visibility = Visibility.Visible;
                    break;
                case CashMachineUiOperation.UiWithdraw:
                    UiGrid_Withdraw.Visibility = Visibility.Visible;
                    break;
                case CashMachineUiOperation.UiDeposit:
                    UiGrid_Deposit.Visibility = Visibility.Visible;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void UiButton_DepositCash_Click(object sender, RoutedEventArgs e)
        {
            UiChangeOperation(CashMachineUiOperation.UiDeposit);

            CashDepositObserver.Clear();
            foreach (var (key, val) in _cashMachine.StoredCash)
            {
                CashDepositObserver.Add(new PairNominalAmount((uint)key, 0));
            }
        }

        private void UiButton_WithdrawCash_Click(object sender, RoutedEventArgs e)
        {
            UiTextBox_WithdrawCashAmount.Text = "0";
            UiChangeOperation(CashMachineUiOperation.UiWithdraw);
        }

        private void UiButton_WithdrawCashAccept_Click(object sender, RoutedEventArgs e)
        {
            var requestedCash = uint.Parse(UiTextBox_WithdrawCashAmount.Text);

            if (requestedCash == 0)
            {
                MessageBox.Show("Невозможно снять 0 купюр", "Операция отменена!");
                return;
            }

            if (requestedCash > _cashMachine.TotalStoredCash)  
            {
                MessageBox.Show("Недостаточно денег в банкомате", "Операция отменена!");
                return;
            }

            var (transactionSuccessful, transactionDetails) = _cashMachine.Withdraw(requestedCash, _useSmallerBills);
            if (!transactionSuccessful)
            {
                MessageBox.Show("Не удалось снять запрошенную сумму", "Операция отменена!");
                return;
            }

            UpdateCashStoredUi();
            UiChangeOperation(CashMachineUiOperation.UiSelect);

            string dictionaryString = string.Join("\n", transactionDetails!.Select(kv => (uint)kv.Key + ": " + kv.Value).ToArray());

            MessageBox.Show($"Успешно снято {requestedCash}\nДетализация операции:\n{dictionaryString}", "Операция успешно завершена!");
        }

        private void UiButton_WithdrawCashCancel_Click(object sender, RoutedEventArgs e)
        {
            UiChangeOperation(CashMachineUiOperation.UiSelect);
        }

        private void UiButton_DepositCashAccept_Click(object sender, RoutedEventArgs e)
        {
            if (CashDepositObserver.All(nominalAmount => nominalAmount.Amount == 0))
            {
                MessageBox.Show("Для зачисления нужно добавить хотя бы 1 купюру", "Операция отменена!");
                return;
            }

            var transaction = new Dictionary<CashNominal, uint>();
            foreach (var nominalAmountPair in CashDepositObserver)
            {
                transaction.Add((CashNominal)nominalAmountPair.Nominal, nominalAmountPair.Amount);
            }

            

            if (!_cashMachine.DepositStackOfMoney(transaction))
            {
                MessageBox.Show("Недостаточно места в бакномате", "Операция отменена!");
                return;
            }
            var total = transaction.Aggregate(0u, (u, nominalAndAmount) => u + (nominalAndAmount.Value * (uint)nominalAndAmount.Key));
            UiChangeOperation(CashMachineUiOperation.UiSelect);
            UpdateCashStoredUi();
            MessageBox.Show($"Успешно зачислено {total}", "Операция успешно завершена!");
        }

        private void UiButton_DepositCashCancel_Click(object sender, RoutedEventArgs e)
        {
            UiChangeOperation(CashMachineUiOperation.UiSelect);
        }

        private void UiTextBox_WithdrawCashAmount_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }


        private bool _useSmallerBills = false;
        private void UiRadioButton_WithdrawCashLargeBills_Checked(object sender, RoutedEventArgs e)
        {
            _useSmallerBills = false;
        }
        private void UiRadioButton_WithdrawCashSmallBills_Checked(object sender, RoutedEventArgs e)
        {
            _useSmallerBills = true;
        }

        private void UiDatagrid_DepositCashCellChanged(object sender, TextCompositionEventArgs e)
        {
            var regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }
    }

    //helper class for updating Datagrid for cash machine deposit
    public class PairNominalAmount : INotifyPropertyChanged
    {
        private uint _nominal;
        private uint _amount;

        public PairNominalAmount(uint nominal, uint amount)
        {
            _nominal = nominal;
            _amount = amount;
        }


        public uint Amount
        {
            get => _amount;
            set
            {
                if (_amount == value) return;
                _amount = value;
                OnPropertyChanged(nameof(Amount));
            }
        }

        public uint Nominal
        {
            get => _nominal;
            set
            {
                if (_nominal == value) return;
                _nominal = value;
                OnPropertyChanged(nameof(Nominal));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    //helper class for updating Datagrid for cash machine status
    public class TupleNominalAmountCapacity : INotifyPropertyChanged
    {
        private uint _nominal;
        private uint _amount;
        private uint _capacity;

        public TupleNominalAmountCapacity(uint nominal, uint amount, uint capacity)
        {
            _nominal = nominal;
            _amount = amount;
            _capacity = capacity;
        }


        public uint Amount
        {
            get => _amount;
            set
            {
                if (_amount == value) return;
                _amount = value;
                OnPropertyChanged(nameof(Amount));
            }
        }

        public uint Nominal
        {
            get => _nominal;
            set
            {
                if (_nominal == value) return;
                _nominal = value;
                OnPropertyChanged(nameof(Nominal));
            }
        }

        public uint Capacity
        {
            get => _capacity;
            set
            {
                if (_capacity == value) return;
                _capacity = value;
                OnPropertyChanged(nameof(Capacity));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }


}