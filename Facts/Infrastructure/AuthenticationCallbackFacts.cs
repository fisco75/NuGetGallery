﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Moq;
using WorldDomination.Web.Authentication;
using WorldDomination.Web.Authentication.Mvc;
using Xunit;

namespace NuGetGallery.Infrastructure
{
    public class AuthenticationCallbackFacts
    {
        public class TheConstructor
        {
            [Fact]
            public void RequiresNonNullUserService()
            {
                ContractAssert.ThrowsArgNull(() => new AuthenticationCallback(null, Mock.Of<IFormsAuthenticationService>()), "userService");
            }

            [Fact]
            public void RequiresNonNullFormsAuthService()
            {
                ContractAssert.ThrowsArgNull(() => new AuthenticationCallback(Mock.Of<IUserService>(), null), "formsAuth");
            }
        }

        public class TheProcessMethod
        {
            [Fact]
            public void RequiresNonNullContext()
            {
                ContractAssert.ThrowsArgNull(() => new TestableAuthenticationCallback().Process(null, new AuthenticateCallbackData()), "context");
            }

            [Fact]
            public void RequiresNonNullModel()
            {
                ContractAssert.ThrowsArgNull(() => new TestableAuthenticationCallback().Process(Mock.Of<HttpContextBase>(), null), "model");
            }

            [Fact]
            public void ThrowsExceptionProvidedInCallbackData()
            {
                // Arrange
                var expected = new Exception("Blargh");
                var httpContext = new Mock<HttpContextBase>().Object;
                var callback = new TestableAuthenticationCallback();
                var model = new AuthenticateCallbackData() {
                    Exception = expected
                };

                // Act
                var actual = Assert.Throws<Exception>(() => callback.Process(httpContext, model));

                // Assert
                Assert.Same(expected, actual);
            }

            [Fact]
            public void ThrowsIfAuthenticatedClientIsNull()
            {
                // Arrange
                var httpContext = new Mock<HttpContextBase>().Object;
                var callback = new TestableAuthenticationCallback();
                var model = new AuthenticateCallbackData();

                // Act
                ContractAssert.ThrowsArgException(
                    () => callback.Process(httpContext, model),
                    "model",
                    "Didn't get any authentication or user data from the OAuth provider?");
            }

            [Fact]
            public void ThrowsIfUserInformationIsNull()
            {
                // Arrange
                var httpContext = new Mock<HttpContextBase>().Object;
                var callback = new TestableAuthenticationCallback();
                var model = new AuthenticateCallbackData()
                {
                    AuthenticatedClient = new AuthenticatedClient("provider")
                };

                // Act
                ContractAssert.ThrowsArgException(
                    () => callback.Process(httpContext, model),
                    "model",
                    "Didn't get any authentication or user data from the OAuth provider?");
            }

            [Fact]
            public void ThrowsIfProviderDidNotProvideId()
            {
                // Arrange
                var httpContext = new Mock<HttpContextBase>().Object;
                var callback = new TestableAuthenticationCallback();
                var model = CreateModel(provider: "foo", id: null, email: "foo@bar.com", userName: "foobar");

                // Act
                ContractAssert.Throws<AuthenticationException>(
                    () => callback.Process(httpContext, model),
                    "Didn't get a user ID from the OAuth provider?");
            }

            [Fact]
            public void SetsAuthCookieIfUserWithOAuthCredentialFound()
            {
                // Arrange
                var httpContext = new Mock<HttpContextBase>().Object;
                var callback = new TestableAuthenticationCallback();
                var model = CreateModel(provider: "windowslive", id: "abc123", email: "foo@bar.com", userName: "foobar");
                var user = new User() { Username = "foobar" };

                callback.MockUserService
                        .Setup(u => u.FindByCredential("oauth:windowslive", "abc123"))
                        .Returns(user);

                // Act
                var result = callback.Process(httpContext, model);

                // Assert
                callback.MockFormsAuth
                        .Verify(f => f.SetAuthCookie("foobar", true, null));
            }

            [Fact]
            public void SetsAuthCookieWithNoRolesIfUserHasEmptyRolesCollection()
            {
                // Arrange
                var httpContext = new Mock<HttpContextBase>().Object;
                var callback = new TestableAuthenticationCallback();
                var model = CreateModel(provider: "windowslive", id: "abc123", email: "foo@bar.com", userName: "foobar");
                var user = new User() { Username = "foobar", Roles = new List<Role>() };

                callback.MockUserService
                        .Setup(u => u.FindByCredential("oauth:windowslive", "abc123"))
                        .Returns(user);

                // Act
                var result = callback.Process(httpContext, model);

                // Assert
                callback.MockFormsAuth
                        .Verify(f => f.SetAuthCookie("foobar", true, null));
            }

            [Fact]
            public void SetsAuthCookieWithRolesIfUserHasRoles()
            {
                // Arrange
                var httpContext = new Mock<HttpContextBase>().Object;
                var callback = new TestableAuthenticationCallback();
                var model = CreateModel(provider: "windowslive", id: "abc123", email: "foo@bar.com", userName: "foobar");
                var user = new User()
                {
                    Username = "foobar",
                    Roles = new List<Role>()
                    {
                        new Role() { Name = "Administrator" },
                        new Role() { Name = "Something Else" }
                    }
                };
                string[] expectedRoles = new [] { "Administrator", "Something Else" };

                callback.MockUserService
                        .Setup(u => u.FindByCredential("oauth:windowslive", "abc123"))
                        .Returns(user);

                // Act
                var result = callback.Process(httpContext, model);

                // Assert
                callback.MockFormsAuth
                        .Verify(f => f.SetAuthCookie(
                            "foobar", 
                            true, 
                            It.Is<IEnumerable<string>>(actualRoles => actualRoles.SequenceEqual(expectedRoles))));
            }

            [Fact]
            public void RedirectsToHomeIfUrlIsEmpty()
            {
                // Arrange
                var httpContext = new Mock<HttpContextBase>().Object;
                var callback = new TestableAuthenticationCallback();
                var model = CreateModel(provider: "windowslive", id: "abc123", email: "foo@bar.com", userName: "foobar");
                var user = new User() { Username = "foobar", Roles = new List<Role>() };

                callback.MockUserService
                        .Setup(u => u.FindByCredential("oauth:windowslive", "abc123"))
                        .Returns(user);

                // Act
                var result = callback.Process(httpContext, model);

                // Assert
                ResultAssert.IsRedirectToRoute(new { 
                    controller = "Pages",
                    action = "Home"
                }, result);
            }

            [Fact]
            public void RedirectsToHomeIfUrlIsAbsolute()
            {
                // Arrange
                var httpContext = new Mock<HttpContextBase>().Object;
                var callback = new TestableAuthenticationCallback();
                var model = CreateModel(provider: "windowslive", id: "abc123", email: "foo@bar.com", userName: "foobar");
                model.RedirectUrl = new Uri("http://hackersrus.com");
                var user = new User() { Username = "foobar", Roles = new List<Role>() };

                callback.MockUserService
                        .Setup(u => u.FindByCredential("oauth:windowslive", "abc123"))
                        .Returns(user);

                // Act
                var result = callback.Process(httpContext, model);

                // Assert
                ResultAssert.IsRedirectToRoute(new
                {
                    controller = "Pages",
                    action = "Home"
                }, result);
            }

            [Fact]
            public void RedirectsToReturnUrlIfLocal()
            {
                // Arrange
                var httpContext = new Mock<HttpContextBase>().Object;
                var callback = new TestableAuthenticationCallback();
                var model = CreateModel(provider: "windowslive", id: "abc123", email: "foo@bar.com", userName: "foobar");
                model.RedirectUrl = new Uri("/safeplace", UriKind.RelativeOrAbsolute);
                var user = new User() { Username = "foobar", Roles = new List<Role>() };

                callback.MockUserService
                        .Setup(u => u.FindByCredential("oauth:windowslive", "abc123"))
                        .Returns(user);

                // Act
                var result = callback.Process(httpContext, model);

                // Assert
                ResultAssert.IsRedirectTo("/safeplace", result);
            }

            [Fact]
            public void RedirectsToLinkPageWithDataInTokenIfNoUserExists()
            {
                const string expectedToken = "foo@bar.com|foobar|abc123|windowslive";

                // Arrange
                var httpContext = new Mock<HttpContextBase>().Object;
                var callback = new TestableAuthenticationCallback();
                var model = CreateModel(provider: "windowslive", id: "abc123", email: "foo@bar.com", userName: "foobar");
                model.RedirectUrl = new Uri("/safeplace", UriKind.RelativeOrAbsolute);
                callback.MockUserService
                        .Setup(u => u.FindByCredential("oauth:windowslive", "abc123"))
                        .Returns((User)null);

                // Act
                var result = callback.Process(httpContext, model);

                // Assert
                ResultAssert.IsRedirectToRoute(new
                {
                    controller = "Authentication",
                    action = "LinkOrCreateUser",
                    token = expectedToken,
                    returnUrl = "/safeplace"
                }, result);
            }

            [Fact]
            public void RedirectsToLinkPageWithNoReturnUrlIfNoUserAndReturnUrlIsAbsolute()
            {
                const string expectedToken = "foo@bar.com|foobar|abc123|windowslive";

                // Arrange
                var httpContext = new Mock<HttpContextBase>().Object;
                var callback = new TestableAuthenticationCallback();
                var model = CreateModel(provider: "windowslive", id: "abc123", email: "foo@bar.com", userName: "foobar");
                model.RedirectUrl = new Uri("http://hackersrus.com");
                callback.MockUserService
                        .Setup(u => u.FindByCredential("oauth:windowslive", "abc123"))
                        .Returns((User)null);

                // Act
                var result = callback.Process(httpContext, model);

                // Assert
                ResultAssert.IsRedirectToRoute(new
                {
                    controller = "Authentication",
                    action = "LinkOrCreateUser",
                    token = expectedToken,
                    returnUrl = (object)null
                }, result);
            }
        }

        private static AuthenticateCallbackData CreateModel(string provider, string id, string email, string userName)
        {
            return new AuthenticateCallbackData()
            {
                AuthenticatedClient = new AuthenticatedClient(provider)
                {
                    UserInformation = new UserInformation()
                    {
                        Id = id,
                        Email = email,
                        UserName = userName
                    }
                }
            };
        }

        public class TestableAuthenticationCallback : AuthenticationCallback
        {
            public Mock<IUserService> MockUserService { get; private set; }
            public Mock<IFormsAuthenticationService> MockFormsAuth { get; private set; }

            public TestableAuthenticationCallback() : base()
            {
                UserService = (MockUserService = new Mock<IUserService>()).Object;
                FormsAuth = (MockFormsAuth = new Mock<IFormsAuthenticationService>()).Object;
            }

            protected override string CalculateToken(string email, string userName, string id, string providerName)
            {
                // Don't use MachineKey.Protect in tests.
                return String.Join("|", email, userName, id, providerName);
            }
        }
    }
}
