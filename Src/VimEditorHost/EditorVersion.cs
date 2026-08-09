using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Vim.EditorHost
{
    /// <summary>
    /// The supported list of editor version
    /// </summary>
    /// <remarks>These must be listed in ascending version order</remarks>
    public enum EditorVersion
    {
        Vs2019,
        Vs2022,
        Vs2026
    }
}
