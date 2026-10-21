using System.Collections;
using System.Collections.Generic;

namespace RestoryMod.E2E.Scenarios;

/// <summary>One end-to-end scenario: a script of steps and checks, run by the harness.</summary>
internal interface IScenario
{
    /// <summary>The request file's other lines (key=value), for scenarios that take options.</summary>
    Dictionary<string, string> Options { set; }

    IEnumerator Run(Driver d);
}
