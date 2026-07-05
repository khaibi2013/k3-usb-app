import urllib.request
import os

SOURCES = [
    # Eicar test rule
    "https://raw.githubusercontent.com/Yara-Rules/rules/master/malware/MALW_Eicar.yar",
    # WannaCry Ransomware
    "https://raw.githubusercontent.com/Yara-Rules/rules/master/malware/RANSOM_WannaCry.yar",
    # Emotet
    "https://raw.githubusercontent.com/Yara-Rules/rules/master/malware/MALW_Emotet.yar",
    # TrickBot
    "https://raw.githubusercontent.com/Yara-Rules/rules/master/malware/MALW_TrickBot.yar",
    # Petya Ransomware
    "https://raw.githubusercontent.com/Yara-Rules/rules/master/malware/RANSOM_Petya.yar"
]

CUSTOM_RULES = """
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
        $mz at 0 and 2 of ($sus*)
}
"""

def main():
    print("Bắt đầu thu thập YARA rules từ cộng đồng...")
    
    combined_rules = [
        "/*",
        "  K3 USB - YARA Rules Database",
        "  Tự động cập nhật từ Github Actions",
        "*/",
        CUSTOM_RULES
    ]
    
    for url in SOURCES:
        try:
            print(f"Đang tải: {url}")
            req = urllib.request.Request(url, headers={'User-Agent': 'Mozilla/5.0'})
            with urllib.request.urlopen(req) as response:
                content = response.read().decode('utf-8')
                
                # Làm sạch rule (xoá các include statement để tránh lỗi khi gộp)
                cleaned = []
                for line in content.split('\n'):
                    if not line.strip().startswith('include') and not line.strip().startswith('import'):
                        cleaned.append(line)
                
                combined_rules.append('\n'.join(cleaned))
                print(" -> Thành công")
        except Exception as e:
            print(f" -> Lỗi: {e}")
            
    # Lưu vào thư mục electron/scanner/k3_rules.yar
    output_path = os.path.join(os.path.dirname(__file__), '..', 'electron', 'scanner', 'k3_rules.yar')
    os.makedirs(os.path.dirname(output_path), exist_ok=True)
    
    with open(output_path, 'w', encoding='utf-8') as f:
        f.write('\n\n'.join(combined_rules))
        
    print(f"Đã tạo cơ sở dữ liệu YARA mới tại: {output_path}")

if __name__ == '__main__':
    main()
