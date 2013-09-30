﻿using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace NuGetGallery
{
    public interface ISimpleCloudBlob
    {
        BlobProperties Properties { get; }
        Uri Uri { get; }
        string Name { get; }
        DateTime LastModifiedUtc { get; }
        string ETag { get; }
        CopyState CopyState { get; }

        Task DeleteIfExistsAsync();
        Task DownloadToStreamAsync(Stream target);
        Task DownloadToStreamAsync(Stream target, AccessCondition accessCondition);
        Task DownloadToFileAsync(string path);

        Task<bool> ExistsAsync();
        Task SetPropertiesAsync();
        Task UploadFromStreamAsync(Stream packageFile);
        Task UploadFromFileAsync(string path);

        Task FetchAttributesAsync();
        Task StartCopyFromBlobAsync(Uri source);
    }
}