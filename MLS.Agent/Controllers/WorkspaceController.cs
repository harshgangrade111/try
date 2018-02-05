﻿using System;
using System.Threading.Tasks;
using Clockwise;
using Microsoft.AspNetCore.Mvc;
using Pocket;
using WorkspaceServer;
using WorkspaceServer.Models.Completion;
using WorkspaceServer.Models.Execution;
using WorkspaceServer.Servers.Scripting;
using static Pocket.Logger<MLS.Agent.Controllers.WorkspaceController>;

namespace MLS.Agent.Controllers
{
    public class WorkspaceController : Controller
    {
#if DEBUG
        private static readonly TimeSpan _defaultBudgetDuration = TimeSpan.FromSeconds(20);
#else
        private static readonly TimeSpan _defaultBudgetDuration = TimeSpan.FromSeconds(7);
#endif

        private readonly WorkspaceServerRegistry workspaceServerRegistry;

        public WorkspaceController(WorkspaceServerRegistry workspaceServerRegistry)
        {
            this.workspaceServerRegistry = workspaceServerRegistry ??
                                           throw new ArgumentNullException(nameof(workspaceServerRegistry));
        }

        [HttpPost]
        [Route("/workspace/run")]
        [Route("/workspace/{DEPRECATED}/compile")] // FIX: (Run) remove this endpoint when Orchestrator no longer calls it
        public async Task<IActionResult> Run(
            [FromBody] RunRequest request)
        {
            using (var operation = Log.ConfirmOnExit())
            {
                RunResult result = null;

                var workspaceType = request.WorkspaceType;

                var budget = new TimeBudget(_defaultBudgetDuration);

                if (string.Equals(workspaceType, "script", StringComparison.OrdinalIgnoreCase))
                {
                    var server = new ScriptingWorkspaceServer();

                    result = await server.Run(request, budget);
                }
                else
                {
                    var server = await workspaceServerRegistry.GetWorkspaceServer(workspaceType);

                    result = await server.Run(request, budget);
                }

                operation.Succeed();

                return Ok(result);
            }
        }

        [HttpPost]
        [Route("/workspace/completion")]
        [Route("/workspace/{DEPRECATED}/getCompletionItems")]
        public async Task<IActionResult> Completion(
            [FromBody] CompletionRequest request)
        {
            using (var operation = Log.ConfirmOnExit())
            {
                var server = new ScriptingWorkspaceServer();

                var result = await server.GetCompletionList(request);

                operation.Succeed();

                return Ok(result);
            }
        }

        [HttpPost]
        [Route("/workspace/diagnostics")]
        [Route("/workspace/{DEPRECATED}/diagnostics")]
        public async Task<IActionResult> Diagnostics(
            [FromBody] RunRequest request)
        {
            using (var operation = Log.ConfirmOnExit())
            {
                var server = new ScriptingWorkspaceServer();

                var result = await server.GetDiagnostics(request);

                operation.Succeed();

                return Ok(result);
            }
        }
    }
}
