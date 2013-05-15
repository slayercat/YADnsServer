YADnsServer
===========
YET ANOTHER DNS SERVER

a small dns server to let you control more. supports  Pan-domain analysis, or query dns server by hostname. it's fully configable with regex.

by default, it uses C:\windows\system32\drivers\etc\e-hosts for it's dns config. e-hosts file is Backward compatible with hosts file, but with serveral other useful function.

config e-hosts
===========



major dns server
==========

program uses '*' for major dns server. if your dns request won't selected by following rules, it'll run to major dns server for request.

you may use Multi-line or one line to config this. all dns server sets there will contains in a list and will use later.

for example:

        * 8.8.8.8 8.8.4.4
        * 114.114.114.114
    
    this two lines indicates we choose 8.8.8.8 8.8.4.4 114.114.114 for major dns server. and is equal to:
        * 8.8.8.8 8.8.4.4 114.114.114


   
choose nameserver by host name
==========

use - to choose the regex for another dns server that matchs this regex, for example:

        - ((\w+|\.)*\.ccsu\.cn) 218.196.40.8 218.196.40.18 218.196.40.9
        
        choose 218.196.40.8 and 218.196.40.18 218.196.40.9 as the dns server of *.ccsu.cn
        (be careful, *.ccsu.cn will not be realized by program, it's not a regex! )
        
        notehere--- such a complex regex will take a hollow long time of request for it contains sub regex!
        
        so you may want to replace it to \w+.ccsu.cn or \w+.\w+.ccsu.cn etc, and never use complex items.


as the example indecate, you can choose multiple nameserver address in one line. but be sure that you can't use two difficult line to do that. the second line just be ignored here.




Pan-domain analysis
==========

use + to indecate that. for example:
        + 66.155.40.250 \w+.wordpress.org
        
    for any regex match this, means something.wordpress.org, will gotten an address  66.155.40.250
    
    again, we match this one by one, until find a regex can be matched, or rollback to another stuff.
    
    
    

old - style hosts file
==========
if there's not any thing indicate it's an regex, it'll taken as plain text and do stuff as hosts do.

        127.0.0.1 localhost
        
        announce localhost as 127.0.0.1



queue of progress
==========
we'll check old - style hosts list first for it's straightforward. then query for any Pan-domain analysis. then turn to choose nameserver by host name. at the very end, check for major dns server.

         127.0.0.1 localhost
         + 66.155.40.250 \w+.wordpress.org
         - ((\w+|\.)*\.ccsu\.cn) 218.196.40.8 218.196.40.18 218.196.40.9
         * 8.8.8.8 8.8.4.4 114.114.114
 
 
check example of e-hosts for more .





about cross the wall in china mainland
==========
I think smarthost [https://code.google.com/p/smarthosts/] is very smart, and program can handle it of course.

still, you may want following two lines for wordpress.
        + 72.233.127.217 \w+.wordpress.com
        + 72.233.127.217 \w+.files.wordpress.com
but besides this, I don't have any clue. if you have something more exciting, please tell me that.




for home or small office use only
==========
it's a small software and not optimize for performance but flexibility. And regex is extremely low efficiency. so do not use it for any Large-scale scene.



licence
==========
use apache licence as licence

program use ARSoft.Tools.Net for progress dns request and response. which is apache licence.

