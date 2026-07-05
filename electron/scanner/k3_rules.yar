/*

  K3 USB - YARA Rules Database

  Tự động cập nhật từ Github Actions

*/


rule suspicious_vbs_dropper {
    meta:
        description = "Mã kịch bản VBScript độc hại"
        author = "K3 USB"
    strings:
        $wscript = "WScript.Shell" nocase
        $http = "MSXML2.XMLHTTP" nocase
        $run = ".Run" nocase
    condition:
        all of them
}

rule malicious_pe_executable {
    meta:
        description = "Phần mềm khả nghi chứa mã độc"
    strings:
        $mz = "MZ"
        $sus1 = "CreateRemoteThread" ascii
        $sus2 = "VirtualAllocEx" ascii
        $sus3 = "WriteProcessMemory" ascii
    condition:
}

rule k3_yara_demo_rule {
    meta:
        description = "Mẫu thử nghiệm YARA cho K3 USB"
    strings:
        $s1 = "THIS_IS_A_YARA_TEST_VIRUS_FOR_K3_USB" ascii
    condition:
        $s1
}


rule eicar 
{
	meta:
		description = "Rule to detect Eicar pattern"
		author = "Marc Rivero | @seifreed"
		hash1 = "275a021bbfb6489e54d471899f7db9d1663fc695ec2fe2a2c4538aabf651fd0f"

	strings:
		$s1 = "X5O!P%@AP[4\\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*" fullword ascii

	condition:
		all of them
}


/*
    This Yara ruleset is under the GNU-GPLv2 license (http://www.gnu.org/licenses/gpl-2.0.html) and open to any user or organization, as    long as you use it under this license.

*/

rule Emotets{
meta:
  author = "pekeinfo"
  date = "2017-10-18"
  description = "Emotets"
strings:
  $mz = { 4d 5a }
  $cmovnz={ 0f 45 fb 0f 45 de }
  $mov_esp_0={ C7 04 24 00 00 00 00 89 44 24 0? }
  $_eax={ 89 E? 8D ?? 24 ?? 89 ?? FF D0 83 EC 04 }
condition:
  ($mz at 0 and $_eax in( 0x2854..0x4000)) and ($cmovnz or $mov_esp_0)
}


/*
    This Yara ruleset is under the GNU-GPLv2 license (http://www.gnu.org/licenses/gpl-2.0.html) and open to any user or organization, as    long as you use it under this license.

*/
rule MALW_trickbot_bankBot : Trojan
{
meta:
 author = "Marc Salinas @Bondey_m"
 description = "Detects Trickbot Banking Trojan"
strings:
$str_trick_01 = "moduleconfig"
$str_trick_02 = "Start"
$str_trick_03 = "Control"
$str_trick_04 = "FreeBuffer"
$str_trick_05 = "Release"
condition:
all of ($str_trick_*)
}
rule MALW_systeminfo_trickbot_module :
Trojan
{
meta:
author = "Marc Salinas @Bondey_m"
description = "Detects systeminfo module from Trickbot Trojan"
strings:
$str_systeminf_01 = "<program>"
$str_systeminf_02 = "<service>"
$str_systeminf_03 = "</systeminfo>"
$str_systeminf_04 =
"GetSystemInfo.pdb"
$str_systeminf_05 = "</autostart>"
$str_systeminf_06 = "</moduleconfig>"
condition:
all of ($str_systeminf_*)
}
rule MALW_dllinject_trickbot_module : Trojan
{
meta:
author = "Marc Salinas @Bondey_m"
description = " Detects dllinject module from Trickbot Trojan"
strings:
$str_dllinj_01 = "user_pref("
$str_dllinj_02 = "<ignore_mask>"
$str_dllinj_03 = "<require_header>"
$str_dllinj_04 = "</dinj>"
condition:
all of ($str_dllinj_*)
}
rule MALW_mailsercher_trickbot_module :
Trojan
{
meta:
author = "Marc Salinas @Bondey_m"
description = " Detects mailsearcher module from Trickbot Trojan"
strings:
$str_mails_01 = "mailsearcher"
$str_mails_02 = "handler"
$str_mails_03 = "conf"
$str_mails_04 = "ctl"
$str_mails_05 = "SetConf"
$str_mails_06 = "file"
$str_mails_07 = "needinfo"
$str_mails_08 = "mailconf"
condition:
all of ($str_mails_*)
}


/*
    This Yara ruleset is under the GNU-GPLv2 license (http://www.gnu.org/licenses/gpl-2.0.html) and open to any user or organization, as    long as you use it under this license.

*/

/*
	Yara Rule Set
	Author: Florian Roth
	Date: 2016-03-24
	Identifier: Petya Ransomware
*/

/* Rule Set ----------------------------------------------------------------- */

rule Petya_Ransomware {
	meta:
		description = "Detects Petya Ransomware"
		author = "Florian Roth"
		reference = "http://www.heise.de/newsticker/meldung/Erpressungs-Trojaner-Petya-riegelt-den-gesamten-Rechner-ab-3150917.html"
		date = "2016-03-24"
		hash = "26b4699a7b9eeb16e76305d843d4ab05e94d43f3201436927e13b3ebafa90739"
	strings:
		$a1 = "<description>WinRAR SFX module</description>" fullword ascii

		$s1 = "BX-Proxy-Manual-Auth" fullword wide
		$s2 = "<!--The ID below indicates application support for Windows 10 -->" fullword ascii
		$s3 = "X-HTTP-Attempts" fullword wide
		$s4 = "@CommandLineMode" fullword wide
		$s5 = "X-Retry-After" fullword wide
	condition:
		uint16(0) == 0x5a4d and filesize < 500KB and $a1 and 3 of ($s*)
}

rule Ransom_Petya {
meta:
    description = "Regla para detectar Ransom.Petya con md5 AF2379CC4D607A45AC44D62135FB7015"
    author = "CCN-CERT"
    version = "1.0"
strings:
    $a1 = { C1 C8 14 2B F0 03 F0 2B F0 03 F0 C1 C0 14 03 C2 }
    $a2 = { 46 F7 D8 81 EA 5A 93 F0 12 F7 DF C1 CB 10 81 F6 }
    $a3 = { 0C 88 B9 07 87 C6 C1 C3 01 03 C5 48 81 C3 A3 01 00 00 }
condition:
    all of them
}
