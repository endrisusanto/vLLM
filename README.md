# Turnkey Private LLM API (vLLM) on Windows WSL2 & Docker (16GB VRAM)

[![Engine: vLLM](https://img.shields.io/badge/Engine-vLLM%200.6+-blue.svg)](https://github.com/vllm-project/vllm)
[![API: OpenAI Compatible](https://img.shields.io/badge/API-OpenAI%20v1%20Compatible-green.svg)](https://platform.openai.com/docs/api-reference)
[![Target GPU: NVIDIA 16GB VRAM](https://img.shields.io/badge/GPU-NVIDIA%2016GB%20VRAM-76B900.svg)](https://www.nvidia.com)
[![Platform: Windows WSL2 / Docker](https://img.shields.io/badge/Platform-Windows%20WSL2%20%7C%20Docker-0078D6.svg)](https://learn.microsoft.com/en-us/windows/wsl/)
[![Networking: Mirrored Mode (ICS Bypass)](https://img.shields.io/badge/Networking-WSL2%20Mirrored%20(ICS%20Bypass)-orange.svg)](https://learn.microsoft.com/en-us/windows/wsl/networking#mirrored-mode-networking)

Paket script otomasi *standalone & turnkey* ("drop-and-run") untuk membangun dan menjalankan server inferensi private Large Language Model (LLM) berbasis **vLLM** berstandar OpenAI API (`/v1`) di lingkungan host Windows dengan akselerasi GPU NVIDIA 16GB VRAM.

Solusi ini dirancang dengan pendekatan rekayasa infrastruktur yang andal untuk lingkungan kerja korporat, mem-bypass batasan ketat jaringan intranet yang **memblokir Internet Connection Sharing (ICS)** melalui konfigurasi **WSL2 Mirrored Networking** dan integrasi otomatis Windows Defender Firewall.

---

## Daftar Isi
- [1. Arsitektur Solusi & Bypass Batasan ICS](#1-arsitektur-solusi--bypass-batasan-ics)
- [2. Spesifikasi Sistem & Persyaratan Hardware](#2-spesifikasi-sistem--persyaratan-hardware)
- [3. Struktur Direktori Proyek](#3-struktur-direktori-proyek)
- [4. Panduan Cepat "Turnkey" (Quickstart)](#4-panduan-cepat-turnkey-quickstart)
- [5. Rekayasa Memori & Tuning 16GB VRAM](#5-rekayasa-memori--tuning-16gb-vram)
- [6. Panduan Download Model Lokal](#6-panduan-download-model-lokal)
- [7. Panduan Koneksi Tim di Jaringan Intranet (LAN)](#7-panduan-koneksi-tim-di-jaringan-intranet-lan)
  - [A. Pengujian cURL (CLI)](#a-pengujian-curl-cli)
  - [B. Pengujian Skrip PowerShell](#b-pengujian-skrip-powershell)
  - [C. Integrasi Python (OpenAI SDK)](#c-integrasi-python-openai-sdk)
  - [D. Integrasi Node.js / TypeScript](#d-integrasi-nodejs--typescript)
  - [E. Integrasi Aplikasi WebUI & IDE Extensions](#e-integrasi-aplikasi-webui--ide-extensions)
- [8. Panduan Konfigurasi Lanjutan (.env & Compose)](#8-panduan-konfigurasi-lanjutan-env--compose)
- [9. Pemantauan & Operasional Harian](#9-pemantauan--operasional-harian)
- [10. Troubleshooting & FAQ](#10-troubleshooting--faq)

---

## 1. Arsitektur Solusi & Bypass Batasan ICS

### Masalah Jaringan Intranet Kantor (ICS Restriction)
Pada instalasi standar Windows, subsistem WSL2 beroperasi di dalam Hyper-V Virtual Switch dengan mode NAT. Mode ini bergantung secara mutlak pada layanan Windows **`SharedAccess` (Internet Connection Sharing / ICS)**.

Pada kebanyakan perusahaan atau institusi dengan kebijakan keamanan tinggi (Active Directory Group Policy / GPO), **layanan ICS diblokir atau dinonaktifkan secara paksa**. Dampaknya:
1. Adapter virtual WSL2 gagal menginisialisasi rute jaringan atau mengalami konflik IP.
2. Port forwarding dari host Windows ke dalam container WSL2 tidak berfungsi.
3. Rekan kerja di subnet LAN yang sama tidak dapat mengakses server API lokal.

### Solusi Rekayasa: WSL2 Mirrored Networking Mode
Paket otomasi ini menyelesaikan masalah di atas secara permanen dengan mengaktifkan fitur modern **WSL2 Mirrored Mode** (`networkingMode=mirrored`) di file `%USERPROFILE%\.wslconfig`:

```mermaid
flowchart TD
    subgraph LAN ["Jaringan Intranet Kantor / LAN Subnet"]
        PeerA["PC Rekan Kerja 1<br/>(Open WebUI / Chatbox)"]
        PeerB["PC Rekan Kerja 2<br/>(Python / Agent / Cline)"]
        PeerC["PC Rekan Kerja 3<br/>(Cursor / VS Code)"]
    end

    subgraph Host ["Host Windows 10/11 (NVIDIA 16GB VRAM)"]
        FW["Windows Defender Firewall<br/>(Inbound Rule Port 8000: ALLOW)"]
        NIC["Physical Network Adapter<br/>(Ethernet / Wi-Fi: 192.168.x.x / 10.x.x.x)"]

        subgraph WSL2 ["Subsistem WSL2 (.wslconfig)"]
            MirrorCore["networkingMode = mirrored<br/>firewall = true<br/>autoProxy = true<br/>(Zero ICS / Direct Interface Binding)"]

            subgraph Docker ["Container Engine (vLLM)"]
                Engine["vLLM Engine (vllm/vllm-openai:latest)<br/>- Port :8000 (OpenAI /v1)<br/>- PagedAttention Cache<br/>- Concurrency: 16"]
                SHM["Shared Memory (IPC: host | shm_size: 8GB)"]
            end
        end

        GPU[("NVIDIA GPU 16GB VRAM<br/>- Model Weights: ~5.5GB (AWQ)<br/>- KV Cache Buffer: ~9.0GB<br/>- OS & DWM Reserve: ~1.5GB")]
    end

    PeerA -->|HTTP POST :8000/v1| FW
    PeerB -->|HTTP POST :8000/v1| FW
    PeerC -->|HTTP POST :8000/v1| FW
    FW --> NIC
    NIC <===>|Interface Mirrored| MirrorCore
    MirrorCore --> Engine
    Engine <===> GPU
    Engine --- SHM
```

### Keunggulan Arsitektur Ini:
- **100% Bebas Ketergantungan ICS:** Tidak menggunakan komponen Virtual Switch NAT Windows.
- **Direct Port Binding:** Port `8000` di dalam container otomatis terikat (*bind*) langsung ke alamat IP fisik host Windows dan `localhost` (`127.0.0.1`).
- **Zero Latency Overhead:** Menghilangkan translasi alamat paket IP antara Linux dan Windows.
- **Firewall Terintegrasi:** Opsi `firewall=true` memastikan Windows Defender Firewall mengontrol lalu lintas masuk dari LAN secara aman.

---

## 2. Spesifikasi Sistem & Persyaratan Hardware

| Komponen | Spesifikasi Minimum | Rekomendasi Pengujian |
| :--- | :--- | :--- |
| **GPU** | NVIDIA GPU 16GB VRAM | NVIDIA RTX 4080 (16GB), RTX 4070 Ti Super (16GB), RTX 5070 Ti (16GB), RTX 3090/4090 |
| **Driver NVIDIA** | Versi 535.xx atau lebih baru (CUDA 12.2+) | NVIDIA Studio/Game Ready Driver terbaru (KMD 617+) |
| **Sistem Operasi** | Windows 11 (Build 22H2+) atau Windows 10 (Build 19045+) | Windows 11 Build 23H2/24H2 dengan WSL 2.0+ |
| **WSL2** | WSL versi 2.0.0 atau lebih tinggi | WSL versi 3.0.x (`wsl --version`) |
| **Container Engine** | Docker Desktop (backend WSL2) atau Podman | Podman Desktop 5.x/6.x atau Docker Desktop v4.25+ |
| **RAM Sistem (Host)** | Minimal 16 GB | 32 GB atau 64 GB DDR4/DDR5 |
| **Penyimpanan** | Minimal 30 GB ruang kosong (SSD NVMe) | SSD NVMe untuk pemuatan bobot model berkecepatan tinggi |

---

## 3. Struktur Direktori Proyek

```
c:\Users\Syncmaster\Desktop\vLLM\
├── .env.example                  # Template variabel lingkungan (Model, Port, VRAM, API Key)
├── .env                          # Konfigurasi aktif operasional engine vLLM
├── docker-compose.yml            # Definisi service vLLM, alokasi GPU NVIDIA, IPC & SHM
├── setup.bat                     # Launcher inisialisasi host & firewall (sekali klik, auto-UAC)
├── start.bat                     # Launcher peluncur server vLLM (sekali klik)
├── stop.bat                      # Launcher penghentian bersih & pelepasan VRAM
├── models/                       # Folder penampung bobot model lokal
│   └── README.md                 # Panduan format model & perintah download via HF Hub CLI
├── scripts/
│   ├── configure-host.ps1        # Otomasi .wslconfig (mirrored mode) & Inbound Windows Firewall
│   ├── detect-model.ps1          # Scanner cerdas folder ./models/ & auto-config kuantisasi
│   ├── healthcheck.ps1           # Polling status kesiapan engine vLLM (/health & /v1/models)
│   ├── setup.ps1                 # Dependency inspector (NVIDIA GPU, WSL2, Docker/Podman)
│   ├── start.ps1                 # Workflow start, deteksi IP LAN, peluncuran compose & dashboard
│   ├── stop.ps1                  # Workflow stop & konfirmasi pelepasan VRAM
│   └── test-chat.ps1             # CLI tester inferensi chat completions (/v1/chat/completions)
└── README.md                     # Dokumentasi teknis & panduan rekan kerja (file ini)
```

---

## 4. Panduan Cepat "Turnkey" (Quickstart)

```mermaid
flowchart LR
    A["1. Klik setup.bat<br/>(Inisialisasi Host)"] --> B["2. Download Model<br/>ke ./models/"]
    B --> C["3. Klik start.bat<br/>(Server Siap)"]
    C --> D["4. Tes & Gunakan<br/>(Lokal & LAN)"]
    D --> E["5. Klik stop.bat<br/>(Lepas VRAM)"]
```

### Langkah 1: Inisialisasi Host (Cukup Sekali)
Klik kanan **`setup.bat`** lalu pilih **Run as administrator** (atau klik dua kali, script akan meminta izin eskalasi UAC secara otomatis):
- Memeriksa ketersediaan GPU NVIDIA dan versi driver via `nvidia-smi`.
- Memeriksa kernel WSL2 dan runtime container (Docker/Podman).
- Mengonfigurasi `%USERPROFILE%\.wslconfig` ke mode `networkingMode=mirrored`.
- Menambahkan aturan Inbound Windows Defender Firewall untuk mengizinkan port `8000`.

> [!NOTE]
> Setelah setup awal, jalankan perintah `wsl --shutdown` di terminal jika diminta, agar konfigurasi baru `.wslconfig` dibaca secara menyeluruh oleh kernel WSL2.

### Langkah 2: Letakkan File Bobot Model ke `./models/`
Unduh model ke dalam subdirektori di dalam folder `./models/` (misalnya model AWQ 7B):
```powershell
pip install huggingface_hub
huggingface-cli download Qwen/Qwen2.5-7B-Instruct-AWQ --local-dir ./models/Qwen2.5-7B-Instruct-AWQ
```

### Langkah 3: Jalankan Server Sekali Klik
Klik dua kali **`start.bat`**:
1. Script `detect-model.ps1` memindai subfolder di `./models/`, mengenali model, dan mengonfigurasi kuantisasi `awq` pada `.env`.
2. Script mendeteksi alamat IP LAN fisik host Anda (contoh: `192.168.1.50`).
3. Container dijalankan via Docker Compose.
4. Polling healthcheck memantau inisialisasi bobot dan KV Cache (~30-45 detik).
5. Layar konsol menampilkan dashboard siap pakai dengan URL endpoint untuk rekan kerja di kantor.

### Langkah 4: Uji Coba Inferensi
Jalankan script pengujian cepat di PowerShell:
```powershell
.\scripts\test-chat.ps1
```

### Langkah 5: Hentikan Server
Klik dua kali **`stop.bat`** saat selesai. Container akan dimatikan secara bersih (*graceful shutdown*) dan seluruh alokasi VRAM akan dikembalikan ke sistem operasi.

---

## 5. Rekayasa Memori & Tuning 16GB VRAM

Menjalankan model LLM di GPU 16GB pada sistem operasi Windows memerlukan manajemen memori yang sangat ketat karena Desktop Window Manager (DWM) dan aplikasi desktop Windows mengonsumsi sekitar 1.0 - 1.5 GB VRAM.

### Simulasi Alokasi Memori (Model 7B/8B AWQ INT4)
$$\text{Total VRAM Tersedia} = 16.303\text{ MiB} \approx 16.0\text{ GB}$$

```
+---------------------------------------------------------------------------------+
|                       TOTAL NVIDIA VRAM: 16.0 GB                                |
+-----------------------------------+-----------------------------+---------------+
| PagedAttention KV Cache (~9.0 GB) | Bobot Model AWQ (~5.5 GB)   | OS/DWM (~1.5GB|
| (Kapasitas Concurrency: 16 reqs)  | (Qwen2.5-7B / Llama-3.1-8B) | (Windows Host)|
+-----------------------------------+-----------------------------+---------------+
|<---------------- vLLM Allocation: 90% (~14.4 GB) -------------->|<-- Reserve -->|
```

### Parameter Kunci di `.env`
- **`GPU_MEMORY_UTILIZATION=0.90`**
  Menginstruksikan vLLM untuk mengalokasikan maksimal 90% dari total VRAM (~14.4 GB). Nilai 10% sisanya (~1.6 GB) disisakan untuk kestabilan driver Windows DWM, display output, dan overhead CUDA runtime.
- **`MAX_MODEL_LEN=8192`**
  Panjang jendela konteks token maksimal. Angka 8192 memberikan ruang yang sangat lega untuk analisis dokumen teknis dan histori percakapan panjang.
- **`MAX_NUM_SEQS=16`**
  Batas maksimal sekuens/request yang diproses secara konkuren. Berkat arsitektur *PagedAttention* vLLM, 16 request dapat dieksekusi simultan tanpa fragmentasi memori.
- **`QUANTIZATION=awq`**
  Menggunakan algoritma *Activation-aware Weight Quantization* (INT4). Memberikan throughput inferensi ~2-3x lebih cepat dibanding FP16 standar dengan degradasi akurasi mendekati nol.

### Matriks Kapasitas Model pada GPU 16GB VRAM

| Model LLM | Format | VRAM Bobot | Sisa VRAM (KV Cache) | Max Concurrency (Context 8k) | Rekomendasi |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Qwen2.5-7B-Instruct** | **AWQ (INT4)** | **~5.5 GB** | **~8.9 GB** | **16 user simultan** | **Pilihan Utama (Cepat, Cerdas, All-round)** |
| **Meta-Llama-3.1-8B-Instruct** | **AWQ (INT4)** | **~6.0 GB** | **~8.4 GB** | **16 user simultan** | **Sangat Baik (Reasoning & English/Indo)** |
| **DeepSeek-R1-Distill-Qwen-7B** | **AWQ (INT4)** | **~5.5 GB** | **~8.9 GB** | **16 user simultan** | **Pilihan Utama untuk Coding & Matematika** |
| **Qwen2.5-14B-Instruct** | **AWQ (INT4)** | **~10.0 GB** | **~4.4 GB** | **4-6 user simultan** | **Penalaran Kompleks (User terbatas)** |
| Mistral-7B-Instruct-v0.3 | AWQ (INT4) | ~5.5 GB | ~8.9 GB | 16 user simultan | Alternatif stabil |

---

## 6. Panduan Download Model Lokal

Model harus memiliki file konfigurasi (`config.json`), tokenizer (`tokenizer.json`), dan bobot (`*.safetensors`).

### Model 1: Qwen2.5-7B-Instruct-AWQ (Sangat Direkomendasikan)
Model serbaguna terbaik dengan pemahaman bahasa Indonesia, coding, dan penulisan instruksi yang sangat kuat:
```powershell
huggingface-cli download Qwen/Qwen2.5-7B-Instruct-AWQ --local-dir ./models/Qwen2.5-7B-Instruct-AWQ
```

### Model 2: Meta-Llama-3.1-8B-Instruct-AWQ
Model open-weights populer dari Meta dengan penalaran umum tingkat tinggi:
```powershell
huggingface-cli download hugging-quants/Meta-Llama-3.1-8B-Instruct-AWQ-INT4 --local-dir ./models/Meta-Llama-3.1-8B-Instruct-AWQ
```

### Model 3: DeepSeek-R1-Distill-Qwen-7B (Reasoning Model)
Model penalaran mendalam (*Chain-of-Thought*) hasil distilasi arsitektur DeepSeek-R1:
```powershell
huggingface-cli download deepseek-ai/DeepSeek-R1-Distill-Qwen-7B --local-dir ./models/DeepSeek-R1-Distill-Qwen-7B
```

### Model 4: Qwen2.5-14B-Instruct-AWQ
Model kapasitas 14 miliar parameter untuk penalaran lebih tinggi (membutuhkan ~10GB VRAM untuk bobot):
```powershell
huggingface-cli download Qwen/Qwen2.5-14B-Instruct-AWQ --local-dir ./models/Qwen2.5-14B-Instruct-AWQ
```

---

## 7. Panduan Koneksi Tim di Jaringan Intranet (LAN)

Setelah server dijalankan via `start.bat`, bagikan parameter berikut ke rekan satu tim di kantor:
- **Base URL:** `http://<IP_LAN_HOST>:8000/v1` (contoh: `http://192.168.1.50:8000/v1`)
- **API Key:** `sk-local-vllm-secret-key` (sesuai setting `API_KEY` di `.env`)
- **Model Name:** Sesuai folder model aktif (contoh: `Qwen2.5-7B-Instruct-AWQ`)

---

### A. Pengujian cURL (CLI)

#### 1. Memeriksa Status Kesiapan (*Health Check*)
```bash
curl http://192.168.1.50:8000/health
# Output: HTTP 200 OK
```

#### 2. Melihat Daftar Model yang Aktif
```bash
curl http://192.168.1.50:8000/v1/models \
  -H "Authorization: Bearer sk-local-vllm-secret-key"
```

#### 3. Mengirim Permintaan Chat Completions (Non-Streaming)
```bash
curl http://192.168.1.50:8000/v1/chat/completions \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer sk-local-vllm-secret-key" \
  -d '{
    "model": "Qwen2.5-7B-Instruct-AWQ",
    "messages": [
      {"role": "system", "content": "Anda adalah asisten teknis yang ringkas."},
      {"role": "user", "content": "Jelaskan prinsip kerja PagedAttention dalam 2 kalimat."}
    ],
    "temperature": 0.7,
    "max_tokens": 256
  }'
```

#### 4. Mengirim Permintaan Streaming Token
```bash
curl http://192.168.1.50:8000/v1/chat/completions \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer sk-local-vllm-secret-key" \
  -d '{
    "model": "Qwen2.5-7B-Instruct-AWQ",
    "messages": [{"role": "user", "content": "Tuliskan puisi pendek tentang teknologi."}],
    "stream": true
  }'
```

---

### B. Pengujian Skrip PowerShell

Rekan kerja di PC Windows dapat menguji langsung tanpa instalasi library tambahan menggunakan skrip yang disertakan:

```powershell
# Uji coba dari localhost:
.\scripts\test-chat.ps1

# Uji coba dari PC rekan kerja di LAN:
.\scripts\test-chat.ps1 -HostAddress 192.168.1.50 -Port 8000 -ApiKey "sk-local-vllm-secret-key"
```

Skrip ini secara otomatis memvalidasi endpoint `/v1/models`, mengukur latensi respons, menghitung token yang dihasilkan, dan menampilkan throughput (*tokens per second*).

---

### C. Integrasi Python (OpenAI SDK)

Instal SDK resmi OpenAI di environment rekan tim:
```bash
pip install openai
```

#### Kode Python Standar & Streaming:
```python
from openai import OpenAI

# Inisialisasi client menunjuk ke IP server vLLM host Anda
client = OpenAI(
    base_url="http://192.168.1.50:8000/v1",
    api_key="sk-local-vllm-secret-key",
)

# 1. Non-Streaming Request
response = client.chat.completions.create(
    model="Qwen2.5-7B-Instruct-AWQ",
    messages=[
        {"role": "system", "content": "Anda adalah senior software architect."},
        {"role": "user", "content": "Jelaskan keuntungan arsitektur microservices vs monolith."},
    ],
    temperature=0.7,
    max_tokens=512,
)
print("--- Response Non-Streaming ---")
print(response.choices[0].message.content)

# 2. Streaming Request (Realtime Output)
print("\n--- Response Streaming ---")
stream = client.chat.completions.create(
    model="Qwen2.5-7B-Instruct-AWQ",
    messages=[
        {"role": "user", "content": "Buatkan fungsi Python untuk quicksort beserta penjelasannya."}
    ],
    temperature=0.7,
    max_tokens=1024,
    stream=True,
)

for chunk in stream:
    content = chunk.choices[0].delta.content or ""
    print(content, end="", flush=True)
print()
```

---

### D. Integrasi Node.js / TypeScript

```bash
npm install openai
```

```typescript
import OpenAI from "openai";

const openai = new OpenAI({
  baseURL: "http://192.168.1.50:8000/v1",
  apiKey: "sk-local-vllm-secret-key",
});

async function main() {
  const completion = await openai.chat.completions.create({
    model: "Qwen2.5-7B-Instruct-AWQ",
    messages: [
      { role: "system", content: "Anda adalah asisten AI yang membantu." },
      { role: "user", content: "Sebutkan 3 tips meningkatkan performa query SQL." }
    ],
    temperature: 0.7,
  });

  console.log(completion.choices[0].message.content);
}

main().catch(console.error);
```

---

### E. Integrasi Aplikasi WebUI & IDE Extensions

#### 1. Open WebUI (Docker)
Aplikasi antarmuka chat bergaya ChatGPT yang kaya fitur:
```bash
docker run -d -p 3000:8080 \
  -e OPENAI_API_BASE_URL=http://192.168.1.50:8000/v1 \
  -e OPENAI_API_KEY=sk-local-vllm-secret-key \
  --name open-webui \
  ghcr.io/open-webui/open-webui:main
```
Akses di browser via `http://localhost:3000`.

#### 2. Chatbox AI (Desktop App - Windows / macOS / Linux)
1. Unduh [Chatbox AI](https://chatboxai.app/).
2. Buka **Settings** -> **AI Provider**.
3. Pilih **OpenAI API Compatible**.
4. Masukkan:
   - **API Host:** `http://192.168.1.50:8000`
   - **API Path:** `/v1`
   - **API Key:** `sk-local-vllm-secret-key`
   - **Model:** `Qwen2.5-7B-Instruct-AWQ`

#### 3. LibreChat (Enterprise Web Platform)
Tambahkan blok berikut pada file `librechat.yaml`:
```yaml
endpoints:
  custom:
    - name: "Office Private LLM"
      apiKey: "sk-local-vllm-secret-key"
      baseURL: "http://192.168.1.50:8000/v1"
      models:
        default: ["Qwen2.5-7B-Instruct-AWQ"]
        fetch: true
      titleConvo: true
```

#### 4. Cline / Roo Code / Continue (VS Code Extensions)
Pada konfigurasi ekstensi:
- **Provider:** `OpenAI Compatible`
- **Base URL:** `http://192.168.1.50:8000/v1`
- **API Key:** `sk-local-vllm-secret-key`
- **Model ID:** `Qwen2.5-7B-Instruct-AWQ`

---

## 8. Panduan Konfigurasi Lanjutan (.env & Compose)

File [`.env`](file:///c:/Users/Syncmaster/Desktop/vLLM/.env) mengendalikan seluruh konfigurasi runtime:

```ini
# ==============================================================================
# 1. Pilihan Model
# ==============================================================================
# Nama subfolder di ./models/ (otomatis diperbarui oleh detect-model.ps1)
MODEL_NAME=Qwen2.5-7B-Instruct-AWQ

# Kuantisasi: awq, gptq, fp8, atau kosongkan untuk model standard
QUANTIZATION=awq

# Tipe data komputasi: auto, float16, bfloat16
DTYPE=auto

# ==============================================================================
# 2. Pengaturan Port & Keamanan
# ==============================================================================
# Port host yang diekspos ke LAN kantor
HOST_PORT=8000

# Kunci API untuk proteksi. Kosongkan jika ingin publik tanpa autentikasi.
API_KEY=sk-local-vllm-secret-key

# ==============================================================================
# 3. Tuning Performa (Preset Optimal: 16GB VRAM)
# ==============================================================================
# Batas rasio VRAM dialokasikan ke vLLM (0.90 = ~14.4 GB)
GPU_MEMORY_UTILIZATION=0.90

# Panjang konteks maksimal
MAX_MODEL_LEN=8192

# Jumlah request konkurensi simultan
MAX_NUM_SEQS=16

# Flag tambahan (misal: --trust-remote-code, --enforce-eager)
EXTRA_ARGS=--trust-remote-code

# Token Hugging Face jika memuat gated model
HF_TOKEN=
```

### Mengubah Port Layanan
Jika port `8000` telah digunakan oleh aplikasi lain di komputer Anda:
1. Ubah `HOST_PORT=8080` di `.env`.
2. Jalankan `setup.bat` untuk memperbarui rule Windows Defender Firewall pada port baru.
3. Jalankan `start.bat`.

---

## 9. Pemantauan & Operasional Harian

### Melihat Log Realtime Engine
Untuk melihat pemrosesan token, aktivitas antrean, atau log error:
```powershell
docker compose logs -f vllm
```

### Memeriksa Penggunaan GPU VRAM
Buka terminal PowerShell terpisah dan pantau konsumsi VRAM secara langsung:
```powershell
nvidia-smi -l 2
```

### Mengganti Model yang Aktif
1. Letakkan folder model baru di dalam `./models/<NAMA_MODEL_BARU>`.
2. Jalankan `stop.bat`.
3. Jalankan `start.bat`. Script akan mendeteksi model baru dan memberikan pilihan menu interaktif jika terdapat lebih dari satu model.

---

## 10. Troubleshooting & FAQ

### Q1: Rekan kerja di LAN tidak bisa menghubungi IP host (Connection Timed Out / Refused)?
1. **Periksa Profil Jaringan Windows:**
   Buka **Settings** -> **Network & internet** -> pastikan profil jaringan Ethernet/Wi-Fi Anda adalah **Private Network** (bukan *Public Network* yang sangat restriktif).
2. **Periksa Rule Firewall:**
   Jalankan perintah ini di PowerShell Administrator:
   ```powershell
   Get-NetFirewallRule -Name "vLLM-API-Port-*" | Select-Object Name, Enabled, Direction, Action
   ```
   Pastikan nilainya `Enabled: True` dan `Action: Allow`.
3. **Konfirmasi Mirrored Mode Aktif:**
   Pastikan file `%USERPROFILE%\.wslconfig` memuat `networkingMode=mirrored`. Jika baru pertama kali diubah, jalankan `wsl --shutdown` di terminal.

### Q2: Terjadi error `CUDA out of memory (OOM)` saat memuat model?
Gejala ini biasanya terjadi jika aplikasi desktop lain (seperti game, Adobe Premiere, browser dengan hardware acceleration tinggi) sedang menyita banyak VRAM sebelum vLLM dimulai.
**Solusi:**
1. Turunkan alokasi memori di `.env`:
   ```ini
   GPU_MEMORY_UTILIZATION=0.85
   ```
2. Atau perpendek panjang konteks:
   ```ini
   MAX_MODEL_LEN=4096
   ```
3. Atau kurangi jumlah sekuens simultan:
   ```ini
   MAX_NUM_SEQS=8
   ```

### Q3: Error `Shared memory size is too small`?
vLLM menggunakan PyTorch multiprocessing dan komunikasi tensor paralel yang memerlukan shared memory besar. Konfigurasi [docker-compose.yml](file:///c:/Users/Syncmaster/Desktop/vLLM/docker-compose.yml) sudah dilengkapi dengan parameter:
```yaml
ipc: host
shm_size: '8gb'
```
Ini memastikan container menggunakan host IPC tanpa limitasi default Docker 64MB.

### Q4: Apakah vLLM dapat dijalankan tanpa internet kantor sama sekali (Air-Gapped)?
**Bisa 100%.** Setelah image Docker `vllm/vllm-openai:latest` telah di-pull ke cache lokal dan file model telah tersimpan lengkap di `./models/<NAMA_MODEL>`, vLLM tidak melakukan panggilan internet eksternal apa pun dan berfungsi penuh secara *offline* di intranet privat Anda.

---

<div align="center">
  <b>Developed by Senior DevOps & Infrastructure Engineering Team</b><br/>
  <i>Engineered for Enterprise Performance, Privacy, and Zero-ICS Intranet Networks.</i>
</div>
