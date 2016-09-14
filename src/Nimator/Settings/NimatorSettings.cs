﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Nimator.Settings
{
    public class NimatorSettings
    {
        private static readonly JsonSerializerSettings jsonSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Objects,
            Formatting = Formatting.Indented,
            Converters = new JsonConverter[] { new Newtonsoft.Json.Converters.StringEnumConverter() },
        };

        internal NimatorSettings()
        {
            this.Layers = new LayerSettings[0];
            this.Notifiers = new NotifierSettings[] { new ConsoleSettings() };
        }

        public NotifierSettings[] Notifiers { get; set; }

        public LayerSettings[] Layers { get; set; }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this, jsonSettings);
        }

        public static NimatorSettings FromJson(string json)
        {
            return JsonConvert.DeserializeObject<NimatorSettings>(json, jsonSettings);
        }

        public static NimatorSettings GetExample()
        {
            return new NimatorSettings
            {
                Notifiers = new NotifierSettings[] 
                { 
                    ConsoleSettings.GetExample(),
                    OpsGenieSettings.GetExample(),
                    SlackSettings.GetExample(),
                },
                Layers = new LayerSettings[]
                {
                    new LayerSettings
                    {
                        Name = "Layer 1",
                        Checks = new ICheckSettings[]
                        {
                            new NoopCheckSettings(),
                            new NoopCheckSettings(),
                        },
                    },
                    new LayerSettings
                    {
                        Name = "Layer 2",
                        Checks = new ICheckSettings[]
                        {
                            new NoopCheckSettings(),
                            new NoopCheckSettings(),
                        },
                    }
                },
            };
        }
    }
}