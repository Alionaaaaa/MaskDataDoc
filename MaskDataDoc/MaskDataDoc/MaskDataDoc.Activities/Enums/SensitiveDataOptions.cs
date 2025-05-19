using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MaskDataDoc.Activities.Enums
{
    [Flags]
    public enum SensitiveDataOptions
    {
        None = 0,
        IDNP = 1,
        Email = 2,
        Phone = 4,
        Password = 8,
        IBAN = 16,
        CreditCard = 32,
        LicensePlate = 64
    }
}