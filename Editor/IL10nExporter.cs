using System.Collections.Generic;

namespace Untitled.ConfigDataBuilder.Editor
{
    public interface IL10nExporter
    {
        void Export(IList<L10nData> l10nList);
    }
}
