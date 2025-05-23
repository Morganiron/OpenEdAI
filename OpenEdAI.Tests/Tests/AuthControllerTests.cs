﻿using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using OpenEdAI.API.Configuration;
using OpenEdAI.API.Controllers;
using OpenEdAI.API.DTOs;

namespace OpenEdAI.Tests.Tests
{
    public class AuthControllerTests
    {
        private static HttpClient CreateHttpClient(HttpStatusCode code, string json)
        {
            // Mock the HttpClient to return a specific status code and JSON response
            var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            handler
              .Protected()
              .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
               )
              .ReturnsAsync(new HttpResponseMessage
              {
                  StatusCode = code,
                  Content = new StringContent(json)
              });
            // Ensure the mock handler is called exactly once
            return new HttpClient(handler.Object)
            {
                BaseAddress = new Uri("https://dummy")
            };
        }

        // Method to get the settings from Appsettings
        private static IOptions<AppSettings> GetSettings()
        {
            return Options.Create(new AppSettings
            {
                AWS = new AWSSettings
                {
                    Cognito = new CognitoSettings
                    {
                        AppClientId = "id",
                        ClientSecret = "secret",
                        RedirectUri = "https://r",
                        Domain = "domain"
                    }
                }
                
            });
        }

        [Fact]
        public async Task ExchangeCode_NonSuccess_ReturnsUnauthorized()
        {
            // Arrange: set up a mock HttpClient that returns a 400 status code
            var factory = new Mock<IHttpClientFactory>();
            factory.Setup(f => f.CreateClient(It.IsAny<string>()))
                   .Returns(CreateHttpClient(HttpStatusCode.BadRequest, "{}"));

            // Set up a mock configuration object
            var settings = GetSettings();

            var logger = new Mock<ILogger<AuthController>>();
            var ctrl = new AuthController(settings, factory.Object, logger.Object);


            // Act: call the ExchangeCode method with a dummy code
            var result = await ctrl.ExchangeCode(new AuthCodeExchangeRequest { Code = "foo" });

            // Assert: check that the result is an UnauthorizedObjectResult
            Assert.IsType<UnauthorizedObjectResult>(result.Result);
        }

        [Fact]
        public async Task ExchangeCode_Success_ReturnsTokens()
        {
            // Arrange: return valid JSON with all three tokens
            var payload = new
            {
                access_token = "A",
                id_token = "I",
                refresh_token = "R"
            };
            var json = JsonSerializer.Serialize(payload);

            // Mock the HttpClient to return a 200 status code and the JSON response
            var factory = new Mock<IHttpClientFactory>();
            factory.Setup(f => f.CreateClient(It.IsAny<string>()))
                   .Returns(CreateHttpClient(HttpStatusCode.OK, json));

            // Set up a mock configuration object
            var settings = GetSettings();

            var logger = new Mock<ILogger<AuthController>>();
            var ctrl = new AuthController(settings, factory.Object, logger.Object);


            // Act: call the ExchangeCode method with a dummy code
            var actionResult = await ctrl.ExchangeCode(new AuthCodeExchangeRequest { Code = "foo" });

            // Assert: check that the result is an OkObjectResult
            Assert.Null(actionResult.Result);
            var tokenResponse = Assert.IsType<AuthTokenResponse>(actionResult.Value);
            Assert.Equal("A", tokenResponse.AccessToken);
            Assert.Equal("I", tokenResponse.IdToken);
            Assert.Equal("R", tokenResponse.RefreshToken);
        }

        [Fact]
        public async Task RefreshToken_NonSuccess_ReturnsUnauthorized()
        {
            // Arrange: set up a mock HttpClient that returns a 401 status code
            var factory = new Mock<IHttpClientFactory>();
            factory.Setup(f => f.CreateClient(It.IsAny<string>()))
                   .Returns(CreateHttpClient(HttpStatusCode.Unauthorized, "{}"));

            // Set up a mock configuration object
            var settings = GetSettings();

            // Create the controller with the mocked configuration and HttpClient
            var logger = new Mock<ILogger<AuthController>>();
            var ctrl = new AuthController(settings, factory.Object, logger.Object);


            // Act: call the RefreshToken method with a dummy refresh token
            var result = await ctrl.RefreshToken(new RefreshTokenRequest { RefreshToken = "x" });

            // Assert: check that the result is an UnauthorizedObjectResult
            Assert.IsType<UnauthorizedObjectResult>(result.Result);
        }

        [Fact]
        public async Task RefreshToken_Success_ParsesTokens()
        {
            // Arrange: create a mock payload with valid tokens
            var payload = new
            {
                access_token = "AX",
                id_token = "IX",
                refresh_token = "RX"
            };

            // Serialize the payload to JSON
            var json = JsonSerializer.Serialize(payload);

            // Mock the HttpClient to return a 200 status code and the JSON response
            var factory = new Mock<IHttpClientFactory>();
            factory.Setup(f => f.CreateClient(It.IsAny<string>()))
                   .Returns(CreateHttpClient(HttpStatusCode.OK, json));

            // Set up a mock configuration object
            var settings = GetSettings();

            // Create the controller with the mocked configuration and HttpClient
            var logger = new Mock<ILogger<AuthController>>();
            var ctrl = new AuthController(settings, factory.Object, logger.Object);

            // Act: call the RefreshToken method with a dummy refresh token
            var actionResult = await ctrl.RefreshToken(new RefreshTokenRequest { RefreshToken = "x" });

            // Assert: check that the result is an OkObjectResult and contains the expected tokens
            Assert.Null(actionResult.Result);
            var refreshResponse = Assert.IsType<AuthTokenResponse>(actionResult.Value);
            Assert.Equal("AX", refreshResponse.AccessToken);
            Assert.Equal("IX", refreshResponse.IdToken);
            Assert.Equal("RX", refreshResponse.RefreshToken);
        }
    }
}
