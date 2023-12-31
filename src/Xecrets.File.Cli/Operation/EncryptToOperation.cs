﻿#region Coypright and GPL License

/*
 * Xecrets File Cli - Copyright © 2022-2023, Svante Seleborg, All Rights Reserved.
 *
 * This code file is part of Xecrets File Cli, parts of which in turn are derived from AxCrypt as licensed under GPL v3 or later.
 * 
 * However, this code is not derived from AxCrypt and is separately copyrighted and only licensed as follows unless
 * explicitly licensed otherwise. If you use any part of this code in your software, please see https://www.gnu.org/licenses/
 * for details of what this means for you.
 *
 * Xecrets File Cli is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
 *
 * Xecrets File Cli is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied
 * warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License along with Xecrets File Cli.  If not, see <https://www.gnu.org/licenses/>.
 *
 * The source repository can be found at https://github.com/ please go there for more information, suggestions and
 * contributions. You may also visit https://www.axantum.com for more information about the author.
*/

#endregion Coypright and GPL License

using AxCrypt.Abstractions;
using AxCrypt.Core.Crypto;
using AxCrypt.Core.Crypto.Asymmetric;

using Xecrets.File.Cli.Abstractions;
using Xecrets.File.Cli.Public;
using Xecrets.File.Cli.Run;
using Xecrets.Licensing.Abstractions;

using static AxCrypt.Abstractions.TypeResolve;

namespace Xecrets.File.Cli.Operation
{
    internal class EncryptToOperation : IExecutionPhases
    {
        public Task<Status> DryAsync(Parameters parameters)
        {
            if (!parameters.Identities.Where(id => id.Passphrase != Passphrase.Empty).Any())
            {
                return Task.FromResult(new Status(XfStatusCode.NoPassword, "A password must be provided to encrypt files."));
            }

            IStandardIoDataStore toFreeStore = parameters.To.FindFree(parameters);
            if (!toFreeStore.VerifyCanWrite(parameters, out Status status))
            {
                return Task.FromResult(status);
            }

            IStandardIoDataStore fromStore = New<IStandardIoDataStore>(parameters.From);
            if (fromStore.IsStdIo && !fromStore.IsNamedStdIo)
            {
                return Task.FromResult(new Status(XfStatusCode.InvalidOption, "Encryption is not supported from an unnamed standard input stream."));
            }

            if (!New<IFileVerify>().CanReadFromFile(fromStore))
            {
                return Task.FromResult(new Status(XfStatusCode.CannotRead, "Can't read from '{0}'.".Format(fromStore.Name)));
            }
            if (!fromStore.IsEncryptable)
            {
                return Task.FromResult(new Status(XfStatusCode.FileUnavailable, "Encryption of '{0}' is not supported, it may be a system file or hidden.".Format(parameters.CurrentOp.From)));
            }

            if (parameters.ProgrammaticUse && FileLargerThanLicenseLimit(fromStore))
            {
                return Task.FromResult(new Status(XfStatusCode.Unlicensed, "'{0}' is too large for encryption. When using options for programmatic use, a valid maintenance subscription is required for files > 1 MB, or use a GPL build.".Format(parameters.CurrentOp.From)));
            }

            parameters.TotalsTracker.AddWorkItem(fromStore.Length());
            return Task.FromResult(Status.Success);
        }

        private static bool FileLargerThanLicenseLimit(IStandardIoDataStore fromStore)
        {
            long length;
            if (fromStore.IsStdIo)
            {
                Stream stdin = fromStore.OpenRead(); // Don't think we should close the stdin stream
                length = stdin.CanSeek ? stdin.Length : 0;
            }
            else
            {
                length = fromStore.Length();
            }

            if (length <= 1024 * 1024)
            {
                return false;
            }
            LicenseStatus licenseStatus = New<ILicense>().Status();
            if (licenseStatus is LicenseStatus.Gpl or LicenseStatus.Valid)
            {
                return false;
            }
            return true;
        }

        public Task<Status> RealAsync(Parameters parameters)
        {
            IStandardIoDataStore toFreeStore = parameters.To.FindFree(parameters);
            if (!toFreeStore.VerifyCanWrite(parameters, out Status status))
            {
                return Task.FromResult(status);
            }

            IStandardIoDataStore fromStore = New<IStandardIoDataStore>(parameters.From);

            parameters.Progress.Display = parameters.From;

            IEnumerable<UserPublicKey> userPublicKeys = parameters.PublicKeys.Where(pk => parameters.SharingEmails.Contains(pk.Email));
            using (var encryption = new Encryption(fromStore.OpenRead(), parameters.Identities.Where(id => id.Passphrase != Passphrase.Empty), userPublicKeys, parameters.Progress))
            {
                encryption.EncryptTo(toFreeStore, fromStore.AliasName);
            }

            string freeTo = Path.Combine(Path.GetDirectoryName(parameters.To) ?? string.Empty, toFreeStore.Name);
            parameters.Logger.Log(new Status(parameters, "Encrypted '{0}' to '{1}'.".Format(parameters.From, freeTo)));
            return Task.FromResult(Status.Success);
        }
    }
}
