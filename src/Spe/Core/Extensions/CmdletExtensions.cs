﻿using System;
using System.Data;
using System.Management.Automation;
using Sitecore;
using Sitecore.Data.Fields;
using Sitecore.Data.Items;
using Sitecore.Exceptions;
using Sitecore.Security.AccessControl;
using Sitecore.Security.Accounts;
using Spe.Commands;
using Spe.Commands.Security;
using AuthorizationManager = Sitecore.Security.AccessControl.AuthorizationManager;

namespace Spe.Core.Extensions
{
    public static class CmdletExtensions
    {
        public static bool CanChangeReadOnly(this Cmdlet command, Item item)
        {
            if (item.Fields[Sitecore.FieldIDs.ReadOnly] == null)
            {
                var error = $"Item '{item.Name}' does not have a ReadOnly field.";
                command.WriteError(new ErrorRecord(new PSInvalidOperationException(error), 
                    ErrorIds.InvalidOperation.ToString(), ErrorCategory.InvalidData, item));
                return false;
            }

            if (!CanUserWrite(item, item.Fields[Sitecore.FieldIDs.ReadOnly], Context.User))
            {
                var error = $"Cannot modify item '{item.Name}' because the ReadOnly field cannot be written.";
                command.WriteError(new ErrorRecord(new SecurityException(error), 
                    ErrorIds.InsufficientSecurityRights.ToString(), ErrorCategory.PermissionDenied,
                    item));
                return false;
            }

            return true;
        }

        private static bool CanUserWrite(Item item, Field field, User user)
        {
            if (AuthorizationManager.GetAccess(field, user, AccessRight.FieldWrite).Permission == AccessPermission.Deny)
                return false;
            if (field.ID == FieldIDs.Security || field.ID == FieldIDs.InheritSecurity)
                return item.Access.CanAdmin();
            return item.Access.CanWrite();
        }

        public static bool CanWrite(this Cmdlet command, Item item)
        {
            if (item.Access.CanWrite()) return true;

            var error = $"Cannot modify item '{item.Name}' because the item cannot be written.";
            command.WriteError(new ErrorRecord(new SecurityException(error), 
                ErrorIds.InsufficientSecurityRights.ToString(), ErrorCategory.PermissionDenied, item));
            return false;
        }

        public static bool CanAdmin(this Cmdlet command, Item item)
        {
            if (item.Access.CanAdmin()) return true;

            var error = $"Item '{item.Name}' cannot be managed by the current user.";
            command.WriteError(new ErrorRecord(new SecurityException(error), 
                ErrorIds.InsufficientSecurityRights.ToString(), ErrorCategory.PermissionDenied, item));
            return false;
        }

        public static bool CanChangeLock(this Cmdlet command, Item item)
        {
            if (item.Locking.HasLock() || item.Locking.CanUnlock() ||
                (!item.Locking.IsLocked() && item.Locking.CanLock())) return true;

            var error = $"Cannot modify item '{item.Name}' because it is locked by '{item.Locking.GetOwner()}'.";
            command.WriteError(new ErrorRecord(new SecurityException(error), 
                ErrorIds.InsufficientSecurityRights.ToString(), ErrorCategory.PermissionDenied, item));
            return false;
        }

        public static bool CanFindAccount(this Cmdlet command, AccountIdentity account, AccountType accountType)
        {
            if (account == null)
            {
                return false;
            }
            var name = account.Name;
            var error = $"Cannot find an account with identity '{name}'.";

            if (accountType == AccountType.Role)
            {
                if (!Role.Exists(name) && !RolesInRolesManager.IsCreatorOwnerRole(name) && !RolesInRolesManager.IsSystemRole(name))
                {
                    command.WriteError(new ErrorRecord(new ObjectNotFoundException(error),
                        ErrorIds.AccountNotFound.ToString(),
                        ErrorCategory.ObjectNotFound, account));
                    return false;
                }
            }

            if (accountType == AccountType.User && !User.Exists(name))
            {
                command.WriteError(new ErrorRecord(new ObjectNotFoundException(error), ErrorIds.AccountNotFound.ToString(),
                    ErrorCategory.ObjectNotFound, account));
                return false;
            }

            return true;
        }

        public static Account GetAccountFromIdentity(this Cmdlet command, AccountIdentity identity)
        {
            Account account = identity;
            if (account == null)
            {
                if (RolesInRolesManager.IsCreatorOwnerRole(identity.Name) || RolesInRolesManager.IsSystemRole(identity.Name))
                {
                    return Role.FromName(identity.Name);
                }
                var error = $"Cannot find an account with identity '{identity.Name}'.";
                command.WriteError(new ErrorRecord(new ObjectNotFoundException(error), ErrorIds.AccountNotFound.ToString(),
                    ErrorCategory.ObjectNotFound, identity));
            }
            return account;
        }

        
        public static bool TryParseAccessRight(this BaseCommand command, string accessRightName, out AccessRight accessRight)
        {
            accessRight = null;

            try
            {
                accessRight = AccessRight.FromName(accessRightName);
            }
            catch (Exception ex)
            {
                command.WriteError(new ErrorRecord(ex, "sitecore_invalid_access_right", ErrorCategory.InvalidArgument, null));
                return false;
            }
            return true;
        }

    }
}