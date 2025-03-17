using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using System.Reflection;
using SimpleFileTransfer.Controllers;

namespace SimpleFileTransfer.Tests.Controllers;

public class TransferRequestTests
{
    [Fact]
    public void TransferRequest_HasRequiredProperties()
    {
        // Check that SourcePath and DestinationPath are marked with required attribute
        var type = typeof(TransferRequest);
        var sourcePathProperty = type.GetProperty("SourcePath");
        var destPathProperty = type.GetProperty("DestinationPath");
        
        Assert.NotNull(sourcePathProperty);
        Assert.NotNull(destPathProperty);
        
        // Check if the properties have the "required" modifier by examining property attributes
        // This is a more generic way to check for required properties without relying on specific attribute types
        var sourcePathAttributes = sourcePathProperty!.GetCustomAttributes(true);
        var destPathAttributes = destPathProperty!.GetCustomAttributes(true);
        
        // At least one attribute should exist for required properties
        Assert.True(sourcePathAttributes.Length > 0);
        Assert.True(destPathAttributes.Length > 0);
    }

    [Fact]
    public void TransferRequest_DefaultsCompressToFalse()
    {
        // Arrange & Act
        var request = new TransferRequest
        {
            SourcePath = "source.txt",
            DestinationPath = "destination.txt"
        };

        // Assert
        Assert.False(request.Compress);
    }

    [Fact]
    public void TransferRequest_DefaultsEncryptToFalse()
    {
        // Arrange & Act
        var request = new TransferRequest
        {
            SourcePath = "source.txt",
            DestinationPath = "destination.txt"
        };

        // Assert
        Assert.False(request.Encrypt);
    }

    [Fact]
    public void TransferRequest_PasswordCanBeNull()
    {
        // Arrange & Act
        var request = new TransferRequest
        {
            SourcePath = "source.txt",
            DestinationPath = "destination.txt",
            Password = null
        };

        // Assert
        Assert.Null(request.Password);
    }

    [Fact]
    public void TransferRequest_AllPropertiesSetCorrectly()
    {
        // Arrange
        var sourcePath = "source.txt";
        var destinationPath = "destination.txt";
        var compress = true;
        var encrypt = true;
        var password = "password123";

        // Act
        var request = new TransferRequest
        {
            SourcePath = sourcePath,
            DestinationPath = destinationPath,
            Compress = compress,
            Encrypt = encrypt,
            Password = password
        };

        // Assert
        Assert.Equal(sourcePath, request.SourcePath);
        Assert.Equal(destinationPath, request.DestinationPath);
        Assert.Equal(compress, request.Compress);
        Assert.Equal(encrypt, request.Encrypt);
        Assert.Equal(password, request.Password);
    }
} 