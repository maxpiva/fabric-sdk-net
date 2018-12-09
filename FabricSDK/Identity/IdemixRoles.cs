using System;
using System.Collections.Generic;
using Hyperledger.Fabric.Protos.Common;

namespace Hyperledger.Fabric.SDK.Identity
{

    /**
     * IdemixRoles is ENUM type that represent a Idemix Role and provide some functionality to operate with Bitmasks
     * and to operate between MSPRoles and IdemixRoles.
     */
    [Flags]
    public enum IdemixRoles
    {
        MEMBER = 1,
        ADMIN = 2,
        CLIENT = 4,
        PEER = 8,
        // Next roles values: 8, 16, 32 ..

    }

    public static class IdemixRolesExtensions
    {
        public static IdemixRoles ToIdemixRoles(this IEnumerable<MSPRole.Types.MSPRoleType> roles)
        {
            IdemixRoles mask = 0;
            foreach (MSPRole.Types.MSPRoleType role in roles)
                mask |= (IdemixRoles) (1 << (int) role);
            return mask;
        }

        public static IdemixRoles ToIdemixRole(this MSPRole.Types.MSPRoleType role)
        {
            if (role < MSPRole.Types.MSPRoleType.Member || role > MSPRole.Types.MSPRoleType.Peer)
                throw new ArgumentException("The provided role is not valid: " + role);
            return (IdemixRoles) (1 << (int) role);
        }

        public static bool CheckRole(this IdemixRoles roles, IdemixRoles searchRole)
        {
            return (roles & searchRole) == searchRole;
        }

        public static List<MSPRole.Types.MSPRoleType> ToMSPRoleTypes(this IdemixRoles roles)
        {
            List<MSPRole.Types.MSPRoleType> ls = new List<MSPRole.Types.MSPRoleType>();
            int pos = 0;
            foreach (IdemixRoles r in Enum.GetValues(typeof(IdemixRoles)))
            {
                if ((r & roles) == r)
                    ls.Add((MSPRole.Types.MSPRoleType) pos);
                pos++;
            }
            return ls;
        }
    }
}
