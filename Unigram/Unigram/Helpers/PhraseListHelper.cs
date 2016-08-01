using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Unigram.Helpers
{
    public class PhraseListHelper
    {
        Windows.ApplicationModel.VoiceCommands.VoiceCommandDefinition commandSetEnUs;

        // Fill the PhraseList for voice commands with the names of the dialogs
        public async void FillPhraseList(List<string> dialogNames)
        {
            if (Windows.ApplicationModel.VoiceCommands.VoiceCommandDefinitionManager.InstalledCommandDefinitions.TryGetValue("CommandSet_en-us", out commandSetEnUs))
            {
                await commandSetEnUs.SetPhraseListAsync("dialogName", dialogNames);
            }
        }

    }
}
