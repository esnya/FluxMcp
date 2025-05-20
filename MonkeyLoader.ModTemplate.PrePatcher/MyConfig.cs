using MonkeyLoader.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MonkeyLoader.ModTemplate
{
    internal sealed class MyConfig : ConfigSection
    {
        private readonly DefiningConfigKey<string> _targetNameKey = new("TargetName", "Your name.", () => "World");

        public override string Description => "This section contains my very useful config options.";

        public override string Id => "Main";

        /// <summary>
        /// Gets or sets the name. Not strictly necessary, but makes it a little nicer to use.
        /// </summary>
        public string TargetName
        {
            get => _targetNameKey.GetValue()!;
            set => _targetNameKey.SetValue(value);
        }

        public override Version Version { get; } = new Version(1, 0, 0);
    }
}