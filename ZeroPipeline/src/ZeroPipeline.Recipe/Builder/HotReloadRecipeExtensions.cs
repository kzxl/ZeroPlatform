using System;
using System.Threading;
using System.Threading.Tasks;
using ZeroPipeline.Core.Execution;
using ZeroPipeline.Recipe.Models;

namespace ZeroPipeline.Recipe.Builder
{
    /// <summary>
    /// Extension methods enabling direct recipe-driven hot-reloading on <see cref="HotReloadPipelineRunner"/>.
    /// </summary>
    public static class HotReloadRecipeExtensions
    {
        /// <summary>
        /// Builds a new pipeline graph from the declarative <see cref="RecipeModel"/> and hot-reloads it into the active runner.
        /// </summary>
        public static Task ReloadRecipeAsync(
            this HotReloadPipelineRunner runner,
            RecipeModel recipe,
            RecipeGraphBuilder? builder = null,
            bool preInitialize = true,
            CancellationToken cancellationToken = default)
        {
            if (runner == null) throw new ArgumentNullException(nameof(runner));
            if (recipe == null) throw new ArgumentNullException(nameof(recipe));

            var graphBuilder = builder ?? new RecipeGraphBuilder();
            var newGraph = graphBuilder.BuildGraph(recipe);

            return runner.ReloadAsync(newGraph, preInitialize, cancellationToken);
        }
    }
}
