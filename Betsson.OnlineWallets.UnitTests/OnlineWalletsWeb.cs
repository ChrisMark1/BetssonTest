using AutoMapper;
using Betsson.OnlineWallets.Exceptions;
using Betsson.OnlineWallets.Models;
using Betsson.OnlineWallets.Services;
using Betsson.OnlineWallets.Web.Controllers;
using Betsson.OnlineWallets.Web.Mappers;
using Betsson.OnlineWallets.Web.Models;
using Betsson.OnlineWallets.Web.Validators;
using FluentValidation.TestHelper;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Assert = Xunit.Assert;

namespace Betsson.OnlineWallets.UnitTests;

public class OnlineWalletControllerTests
{
    private readonly Mock<IOnlineWalletService> _walletServiceMock = new();
    private readonly Mock<IMapper> _mapperMock = new();
    private readonly Mock<ILogger<OnlineWalletController>> _loggerMock = new();
    private readonly OnlineWalletController _controller;
    private readonly DepositRequestValidator _depositRequestValidator = new ();
    private readonly WithdrawalRequestValidator _withdrawalRequestValidator = new ();
    
    private readonly SystemController  _sysController = new ();
    private readonly DefaultHttpContext _context = new ();
   
    public OnlineWalletControllerTests()
    {
        _controller = new OnlineWalletController(_loggerMock.Object, _mapperMock.Object, _walletServiceMock.Object);
    }

    [Fact]
    public async Task Deposit_WithValidRequest_ReturnsCorrectBalanceResponse()
    {
        // Arrange
        var mockOnlineWalletService = new Mock<IOnlineWalletService>();
        var depositRequest = new DepositRequest { Amount = 100 };
        var mappedDeposit = new Deposit { Amount = 100 };
        var returnedBalance = new Balance { Amount = 200 };
        var expectedResponse = new BalanceResponse { Amount = 200 };

        _mapperMock.Setup(m => m.Map<Deposit>(It.IsAny<DepositRequest>())).Returns(mappedDeposit);
        _walletServiceMock.Setup(s => s.DepositFundsAsync(It.IsAny<Deposit>())).ReturnsAsync(returnedBalance);
        _mapperMock.Setup(m => m.Map<BalanceResponse>(It.IsAny<Balance>())).Returns(expectedResponse);

        var controller = new OnlineWalletController(_loggerMock.Object, _mapperMock.Object, mockOnlineWalletService.Object);

        //Add depositRequestValidator to the controller
        var result = await controller.Deposit(depositRequest);

        // Assertion of valid response
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(expectedResponse, okResult.Value);
    }
    
    [Fact]
    public async Task Withdraw_WithValidRequest_ReturnsCorrectBalanceResponse()
    {
        // Arrange
        var withdrawalRequest = new WithdrawalRequest { Amount = 50 };
        var mappedWithdrawal = new Withdrawal { Amount = 50 };
        var returnedBalance = new Balance { Amount = 150 };
        var expectedResponse = new BalanceResponse { Amount = 150 };

        _mapperMock.Setup(m => m.Map<Withdrawal>(It.IsAny<WithdrawalRequest>())).Returns(mappedWithdrawal);
        _walletServiceMock.Setup(s => s.WithdrawFundsAsync(It.IsAny<Withdrawal>())).ReturnsAsync(returnedBalance);
        _mapperMock.Setup(m => m.Map<BalanceResponse>(It.IsAny<Balance>())).Returns(expectedResponse);

        var controller = new OnlineWalletController(_loggerMock.Object, _mapperMock.Object, _walletServiceMock.Object);

        //Add withdrawRequestValidator to the controller
        var result = await controller.Withdraw(withdrawalRequest);

        // Assertion of valid response
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(expectedResponse, okResult.Value);
    }
    
    [Fact]
    public void Error_HandlesInsufficientBalanceException_ReturnsBadRequest()
    {
        _context.Features.Set<IExceptionHandlerPathFeature>(new ExceptionHandlerFeature
        {
            Error = new InsufficientBalanceException("Test exception")
        });
        _sysController.ControllerContext = new ControllerContext { HttpContext = _context };

        var result = _sysController.Error();

        var problemResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, problemResult.StatusCode);
    }
    
    [Fact]
    public void Error_HandlesUnknownException_ReturnsInternalServerError()
    {
        _sysController.ControllerContext = new ControllerContext { HttpContext = _context };

        var result = _sysController.Error();

        var problemResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, problemResult.StatusCode);
    }
    
    [Fact]
    public async Task Balance_ReturnsBalanceResponse()
    {
        var mockService = new Mock<IOnlineWalletService>();
        mockService.Setup(s => s.GetBalanceAsync()).ReturnsAsync(new Balance { Amount = 100m });
        var controller = new OnlineWalletController(null, new MapperConfiguration(cfg => cfg.AddProfile<OnlineWalletMappingProfile>()).CreateMapper(), mockService.Object);
        var result = await controller.Balance();

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var balanceResponse = Assert.IsType<BalanceResponse>(okResult.Value);
        Assert.Equal(100m, balanceResponse.Amount);
    }

    #region Extreme Cases of amounts to maximize test coverage for validator
    
    [Fact]
    public void Validate_DepositRequestWithHighAmount_ShouldPass()
    {
        var model = new DepositRequest { Amount = 1000000m };
        var result = _depositRequestValidator.TestValidate(model);
        result.ShouldNotHaveValidationErrorFor(request => request.Amount);
    }

    [Fact]
    public void Validate_DepositRequestWithLowPositiveAmount_ShouldPass()
    {
        var model = new DepositRequest { Amount = 0.01m };
        var result = _depositRequestValidator.TestValidate(model);
        result.ShouldNotHaveValidationErrorFor(request => request.Amount);
    }

    [Fact]
    public void Validate_DepositRequestWithHighPrecisionAmount_ShouldPass()
    {
        var model = new DepositRequest { Amount = 100.12345678m };
        var result = _depositRequestValidator.TestValidate(model);
        result.ShouldNotHaveValidationErrorFor(request => request.Amount);
    }
    
    [Fact]
    public void Validate_WithdrawalRequestWithHighAmount_ShouldPass()
    {
        var model = new WithdrawalRequest { Amount = 1000000m };
        var result = _withdrawalRequestValidator.TestValidate(model);
        result.ShouldNotHaveValidationErrorFor(request => request.Amount);
    }

    [Fact]
    public void Validate_WithdrawalRequestWithLowPositiveAmount_ShouldPass()
    {
        var model = new WithdrawalRequest { Amount = 0.01m };
        var result = _withdrawalRequestValidator.TestValidate(model);
        result.ShouldNotHaveValidationErrorFor(request => request.Amount);
    }

    [Fact]
    public void Validate_WithdrawalRequestWithHighPrecisionAmount_ShouldPass()
    {
        var model = new WithdrawalRequest { Amount = 100.12345678m };
        var result = _withdrawalRequestValidator.TestValidate(model);
        result.ShouldNotHaveValidationErrorFor(request => request.Amount);
    }
    
    #endregion
    
}