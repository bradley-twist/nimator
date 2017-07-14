﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace Nimator
{
    /// <summary>
    /// Core <see cref="INimatorEngine"/> that will run layers sequentially.
    /// </summary>
    public class NimatorEngine : INimatorEngine
    {
        private const NotificationLevel StopProcessingAtThreshold = NotificationLevel.Error;

        private readonly IList<ILayer> layers;
        
        /// <summary>
        /// Constructs default engine without any <see cref="ILayer"/>s.
        /// </summary>
        public NimatorEngine()
        {
            this.layers = new List<ILayer>();
        }

        /// <summary>
        /// Constructs engine with specifica <see cref="ILayer"/>s.
        /// </summary>
        /// <param name="layers"></param>
        public NimatorEngine(IEnumerable<ILayer> layers)
        {
            if (layers == null) throw new ArgumentNullException(nameof(layers));
            this.layers = layers.ToList();
        }

        /// <inheritDoc/>
        public INimatorResult RunSafe()
        {
            try
            {
                return RunUnsafe();
            }
            catch (AggregateException ex)
            {
                var fullText = $"Nimator itself failed: {GetAggregateExceptionMessage(ex)}";

                return new CriticalNimatorResult("Nimator (or one of its layers) itself failed.", fullText);
            }
            catch (Exception ex)
            {
                var fullText = $"Nimator itself failed: {GetInnerExceptionMessage(ex)}";

                return new CriticalNimatorResult("Nimator (or one of its layers) itself failed.", fullText);
            }
        }

        private NimatorResult RunUnsafe()
        {
            var nimatorResult = new NimatorResult(AmbientTimeProvider.GetNow());
            
            foreach (var layer in this.layers)
            {
                var layerResult = layer.Run();

                if (layerResult == null)
                {
                    throw new InvalidOperationException("Layer " + layer.Name + " returned no result. Cannot continue because we now cannot determine error level of that layer.");
                }

                nimatorResult.LayerResults.Add(layerResult);

                if (layerResult.Level >= StopProcessingAtThreshold)
                {
                    break;
                }
            }

            nimatorResult.Finished = AmbientTimeProvider.GetNow();

            return nimatorResult;
        }

        private string GetInnerExceptionMessage(Exception ex)
        {
            if (ex == null)
            {
                return string.Empty;
            }

            string message = ex.Message + Environment.NewLine;

            if (ex.InnerException != null)
            {
                message += '\t' + GetInnerExceptionMessage(ex.InnerException);
            }

            return message;
        }

        private string GetAggregateExceptionMessage(AggregateException ex)
        {
            if (ex == null)
            {
                return string.Empty;
            }

            string message = "";

            foreach (var innerEx in ex.InnerExceptions)
            {
                message += '\t' + innerEx.Message;
            }

            return message;
        }

        /// <inheritDoc/>
        public void AddLayer(string name, IEnumerable<ICheck> checks)
        {
            this.layers.Add(new Layer(name, checks));
        }

        /// <inheritDoc/>
        public void AddLayer(ILayer layer)
        {
            this.layers.Add(layer);
        }
    }
}
