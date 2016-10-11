import os, re

d = "unidecode\unidecode"
print "working..."

fp = open("..\Characters.cs","w")
fp.write('''using System;
using System.Collections.Generic;

namespace Unigram.Core.Unidecode 
{
    public static partial class Unidecoder
    {
        private static readonly Dictionary<int, string[]> characters;
        static Unidecoder()
        {
            characters = new Dictionary<int, string[]> 
			{
''')

def formatch(ch, cc):
	ch = ch.replace("\r", "")
	ch = ch.replace("\\", "\\\\")
	ch = ch.replace("\"", "\\\"")
	ch = ch.replace("\n", '"+Environment.NewLine+"')
	return ch if cc > 31 else "\\u"+('%x' % cc).rjust(4,'0')
	
for file in [file for file in os.listdir(d) if not file in [".",".."]]:
	m = re.search('x(.{3})\.py$', file)
	if m:
		data = __import__(d+"."+file[0:-3], [], [], ['data']).data
		c = 0
		num = int(m.group(1), 16)*256
		fp.write('                {%s /*%s %s*/, new[]{\n'%(int(m.group(1), 16), num, m.group(1)))
		for ch in data:
			fp.write('"%s" /*%s*/%s '%(
				formatch(ch, num+c),
				("%x"%(num+c)).rjust(4,'0'),
				","if c<255 else ""))
			c=c+1
		fp.write('}},\n\n')

fp.write(
'''            };
        }
    }
}
''')

