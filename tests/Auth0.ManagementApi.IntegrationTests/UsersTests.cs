﻿using System;
using System.Threading.Tasks;
using Auth0.Core;
using Auth0.Core.Exceptions;
using Auth0.ManagementApi.Models;
using FluentAssertions;
using NUnit.Framework;
using Auth0.Tests.Shared;

namespace Auth0.ManagementApi.IntegrationTests
{
    [TestFixture]
    public class UsersTests : TestBase
    {
        private ManagementApiClient apiClient;
        private Connection connection;

        [SetUp]
        public async Task SetUp()
        {
            var scopes = new
            {
                users = new
                {
                    actions = new string[] { "read", "create", "update", "delete" }
                },
                connections = new
                {
                    actions = new string[] { "create", "delete" }
                },
                users_app_metadata = new
                {
                    actions = new string[] { "update" }
                }

            };
            string token = GenerateToken(scopes);

            apiClient = new ManagementApiClient(token, new Uri(GetVariable("AUTH0_MANAGEMENT_API_URL")));

            // We will need a connection to add the users to...
            connection = await apiClient.Connections.Create(new ConnectionCreateRequest
            {
                Name = Guid.NewGuid().ToString("N"),
                Strategy = "auth0",
                EnabledClients = new[] { "rLNKKMORlaDzrMTqGtSL9ZSXiBBksCQW" }
            });
        }

        [TearDown]
        public async Task TearDown()
        {
            await apiClient.Connections.Delete(connection.Id);
        } 

        [Test]
        public async Task Test_users_crud_sequence()
        {
            // Get all the users
            var usersBefore = await apiClient.Users.GetAll();

            // Add a new user
            var newUserRequest = new UserCreateRequest
            {
                Connection = connection.Name,
                Email = $"{Guid.NewGuid().ToString("N")}@nonexistingdomain.aaa",
                EmailVerified = true,
                Password = "password"
            };
            var newUserResponse = await apiClient.Users.Create(newUserRequest);
            newUserResponse.Should().NotBeNull();
            newUserResponse.Email.Should().Be(newUserRequest.Email);

            // Get all the users again. Verify we now have one more
            var usersAfter = await apiClient.Users.GetAll();
            usersAfter.Count.Should().Be(usersBefore.Count + 1);

            // Update the user
            var updateUserRequest = new UserUpdateRequest
            {
                Email = $"{Guid.NewGuid().ToString("N")}@nonexistingdomain.aaa",
                VerifyEmail = false
            };
            var updateUserResponse = await apiClient.Users.Update(newUserResponse.UserId, updateUserRequest);
            updateUserResponse.Should().NotBeNull();
            updateUserResponse.Email.Should().Be(updateUserRequest.Email);

            // Get a single user
            var user = await apiClient.Users.Get(newUserResponse.UserId);
            user.Should().NotBeNull();
            user.Email.Should().Be(updateUserResponse.Email);

            // Delete the user and ensure we get an exception when trying to fetch them again
            await apiClient.Users.Delete(user.UserId);
            Func<Task> getFunc = async () => await apiClient.Users.Get(user.UserId);
            getFunc.ShouldThrow<ApiException>().And.ApiError.ErrorCode.Should().Be("inexistent_user");
        }

        [Test]
        public async Task Test_user_blocking()
        {
            // Add a new user, and ensure user is not blocked
            var newUserRequest = new UserCreateRequest
            {
                Connection = connection.Name,
                Email = $"{Guid.NewGuid().ToString("N")}@nonexistingdomain.aaa",
                EmailVerified = true,
                Password = "password"
            };
            var newUserResponse = await apiClient.Users.Create(newUserRequest);
            newUserResponse.Blocked.Should().BeFalse();

            // Ensure the user is not blocked when we select the user individually
            var user = await apiClient.Users.Get(newUserResponse.UserId);
            user.Blocked.Should().BeFalse();

            // Block the user, and ensure returned user is blocked
            var updateUserRequest = new UserUpdateRequest
            {
                Blocked = true
            };
            var updateUserResponse = await apiClient.Users.Update(newUserResponse.UserId, updateUserRequest);
            updateUserResponse.Blocked.Should().BeTrue();

            // Ensure the user is not blocked when we select the user individually
            user = await apiClient.Users.Get(newUserResponse.UserId);
            user.Blocked.Should().BeTrue();

            // Delete the user
            await apiClient.Users.Delete(user.UserId);
        }

        [Test]
        public async Task Test_pagination_totals_deserialize_correctly()
        {
            var users = await apiClient.Users.GetAll(includeTotals: true);

            users.Should().NotBeNull();
            users.Paging.Should().NotBeNull();
        }
    }
}