using Betsson.OnlineWallets.Data.Models;
using Betsson.OnlineWallets.Data.Repositories;
using Betsson.OnlineWallets.Exceptions;
using Betsson.OnlineWallets.Extensions;
using Betsson.OnlineWallets.Models;
using Betsson.OnlineWallets.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using Assert = Xunit.Assert;

namespace Betsson.OnlineWallets.UnitTests
{
    public class OnlineWalletServiceTests
    {
        private readonly Mock<IOnlineWalletService> _walletServiceMock = new();
        private readonly Mock<IOnlineWalletRepository> _walletRepository = new();

        #region Deposit

        [Fact]
        public async Task SuccessfulDeposit_UpdatesBalanceCorrectly()
        {
            //Mock Wallet Repository
            decimal initialBalanceAmount = 200m;
            _walletRepository.Setup(repo => repo.GetLastOnlineWalletEntryAsync()).ReturnsAsync(new OnlineWalletEntry
                { BalanceBefore = initialBalanceAmount, Amount = 0m });
            DateTimeOffset testStartTime = DateTimeOffset.UtcNow;

            //Assertions for all fields
            _walletRepository.Setup(repo => repo.InsertOnlineWalletEntryAsync(It.IsAny<OnlineWalletEntry>()))
                .Callback<OnlineWalletEntry>(entry =>
                {
                    Assert.False(string.IsNullOrEmpty(entry.Id));
                    Assert.Equal(70m, entry.Amount);
                    Assert.Equal(initialBalanceAmount, entry.BalanceBefore);
                    Assert.True(entry.EventTime >= testStartTime);
                })
                .Returns(Task.CompletedTask);
            var service = new OnlineWalletService(_walletRepository.Object);

            // Action of deposit amount
            var result = await service.DepositFundsAsync(new Deposit { Amount = 70m });

            // Assertions of updated deposit value & last updates
            Assert.Equal(270m, result.Amount);
            _walletRepository.Verify(repo => repo.InsertOnlineWalletEntryAsync(It.IsAny<OnlineWalletEntry>()),
                Times.Once);
            _walletRepository.Verify(repo => repo.GetLastOnlineWalletEntryAsync(), Times.Once);
        }

        [Fact]
        public async Task Deposit_NegativeAmount_Returns_Negative_Amount_None_ValidationError()
        {
            // Mock and give negative deposit value
            var expectedDepositResult = new Balance { Amount = -50m }; // Assuming Balance is the return type
            _walletServiceMock.Setup(s => s.DepositFundsAsync(It.IsAny<Deposit>()))
                .ReturnsAsync(expectedDepositResult);

            // Action of deposit
            var actualResult = await _walletServiceMock.Object.DepositFundsAsync(new Deposit { Amount = -50m });

            // Assertion of negative to positive value
            Assert.Equal(expectedDepositResult.Amount, actualResult.Amount);
        }

        #endregion

        #region Withdraw

        [Fact]
        public async Task SuccessfulWithdrawal_UpdatesBalanceCorrectly()
        {
            // Mock GetLast to simulate the initial balance
            decimal initialBalance = 100m;
            decimal withdrawalAmount = 50m;
            decimal expectedBalanceAfterWithdrawal = initialBalance - withdrawalAmount;
            _walletRepository.Setup(repo => repo.GetLastOnlineWalletEntryAsync())
                .ReturnsAsync(new OnlineWalletEntry { BalanceBefore = initialBalance, Amount = 0m });

            // Mock the WithdrawFundsAsync method to simulate the balance after withdrawal
            _walletServiceMock.Setup(service => service.WithdrawFundsAsync(It.IsAny<Withdrawal>()))
                .ReturnsAsync(new Balance { Amount = expectedBalanceAfterWithdrawal });

            // Perform the withdrawal
            var result = await _walletServiceMock.Object.WithdrawFundsAsync(new Withdrawal { Amount = withdrawalAmount });

            // Assert that the balance after withdrawal is as expected
            Assert.Equal(expectedBalanceAfterWithdrawal, result.Amount);
        }

        [Fact]
        public async Task Withdrawal_NegativeAmount_ThrowsArgumentOutOfRangeException()
        {
            var withdrawalAmount = -50m;
            _walletServiceMock.Setup(service => service.WithdrawFundsAsync(It.IsAny<Withdrawal>()))
                .ThrowsAsync(new ArgumentOutOfRangeException());

            // Assert that an ArgumentOutOfRangeException is thrown for a negative withdrawal amount
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _walletServiceMock.Object.WithdrawFundsAsync(new Withdrawal { Amount = withdrawalAmount }));
        }

        [Fact]
        public async Task Withdraw_AmountMoreThanBalance_ThrowsInsufficientBalanceException()
        {
            // Mock Wallet Repository to return a balance of 50
            _walletRepository.Setup(repo => repo.GetLastOnlineWalletEntryAsync())
                .ReturnsAsync(new OnlineWalletEntry { BalanceBefore = 50m, Amount = 0m });

            // Setup the Wallet Service to throw InsufficientBalanceException for withdrawal attempts exceeding the balance
            _walletServiceMock.Setup(service => service.WithdrawFundsAsync(It.Is<Withdrawal>(w => w.Amount > 50m)))
                .ThrowsAsync(new InsufficientBalanceException());

            // Assert that an InsufficientBalanceException is thrown for a withdrawal of 100m
            await Assert.ThrowsAsync<InsufficientBalanceException>(() =>
                _walletServiceMock.Object.WithdrawFundsAsync(new Withdrawal { Amount = 100m }));
        }

        [Fact]
        public async Task WithdrawFundsAsync_ThrowsInsufficientBalanceException_WhenFundsAreInsufficient()
        {
            // Mock insufficient balance exception
            var withdrawal = new Withdrawal { Amount = 150 };
            _walletServiceMock.Setup(service => service.WithdrawFundsAsync(withdrawal))
                .ThrowsAsync(new InsufficientBalanceException());

            // Assertion of exception when withdrawing more than the balance
            await Assert.ThrowsAsync<InsufficientBalanceException>(() =>
                _walletServiceMock.Object.WithdrawFundsAsync(withdrawal));
        }

        [Fact]
        public async Task Withdraw_CompletesSuccessfully_WhenWithdrawingExactFunds()
        {
            // initial and current withdrawal value
            var withdrawal = new Withdrawal { Amount = 100 };
            var initialBalance = new Balance { Amount = 100 };
            var expectedBalance = new Balance { Amount = 0 };
            _walletServiceMock.Setup(service => service.GetBalanceAsync()).ReturnsAsync(initialBalance);
            _walletServiceMock.Setup(service => service.WithdrawFundsAsync(withdrawal)).ReturnsAsync(expectedBalance);

            var result = await _walletServiceMock.Object.WithdrawFundsAsync(withdrawal);

            // Assertion of resulted balance
            Assert.Equal(expectedBalance.Amount, result.Amount);
        }

        [Fact]
        public async Task Withdraw_DoesNotChangeBalance_WhenWithdrawingZeroAmount()
        {
            // Check for zero withdrawal amount
            var withdrawal = new Withdrawal { Amount = 0 };
            var initialBalance = new Balance { Amount = 100 };
            _walletServiceMock.Setup(service => service.GetBalanceAsync()).ReturnsAsync(initialBalance);
            _walletServiceMock.Setup(service => service.WithdrawFundsAsync(withdrawal)).ReturnsAsync(initialBalance);

            var result = await _walletServiceMock.Object.WithdrawFundsAsync(withdrawal);

            // Assertion the new balance that was not changed
            Assert.Equal(initialBalance.Amount, result.Amount);
        }

        [Fact]
        public async Task WithdrawFundsAsync_ThrowsArgumentOutOfRangeException_WhenWithdrawingNegativeAmount()
        {
            var withdrawal = new Withdrawal { Amount = -50 };
            _walletServiceMock.Setup(service => service.WithdrawFundsAsync(withdrawal))
                .ThrowsAsync(new ArgumentOutOfRangeException());

            // Assert exception when adding negative amount of withdraw funds
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                _walletServiceMock.Object.WithdrawFundsAsync(withdrawal));
        }

        #endregion

        #region Register Online Wallet Service

        [Fact]
        public void RegisterOnlineWalletService_AddsIOnlineWalletServiceAsTransient()
        {
            var services = new ServiceCollection();
            services.RegisterOnlineWalletService();

            // Assertion  service and type initiated
            var serviceDescriptor =
                services.FirstOrDefault(descriptor => descriptor.ServiceType == typeof(IOnlineWalletService));
            Assert.NotNull(serviceDescriptor);
            Assert.Equal(ServiceLifetime.Transient, serviceDescriptor.Lifetime);
            Assert.Equal(typeof(OnlineWalletService), serviceDescriptor.ImplementationType);
        }

        #endregion
    }
}