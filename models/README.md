# Panduan Direktori & Download Model Lokal (16GB VRAM)

Folder `./models/` digunakan sebagai tempat penyimpanan bobot model LLM lokal. Folder ini akan di-mount secara langsung ke dalam container vLLM (`/models:ro`).

---

## 1. Struktur Folder Model yang Benar

Setiap model harus diletakkan di dalam subdirektori tersendiri di bawah `./models/`:

```
vLLM/
└── models/
    ├── Qwen2.5-7B-Instruct-AWQ/
    │   ├── config.json
    │   ├── generation_config.json
    │   ├── model.safetensors (atau model-*.safetensors)
    │   ├── tokenizer.json
    │   └── tokenizer_config.json
    │
    └── Meta-Llama-3.1-8B-Instruct-AWQ/
        ├── config.json
        └── ...
```

---

## 2. Model Paling Direkomendasikan untuk GPU 16GB VRAM

Untuk GPU 16GB VRAM (RTX 4080, RTX 4070 Ti Super, RTX 5070 Ti), model terkuantisasi **AWQ (INT4)** atau **FP8** memberikan rasio kecepatan, efisiensi memori, dan kualitas penalaran terbaik:

| Model Repository Hugging Face | VRAM Bobot | Sisa VRAM (KV Cache) | Rekomendasi Penggunaan |
| :--- | :--- | :--- | :--- |
| `Qwen/Qwen2.5-7B-Instruct-AWQ` | ~5.5 GB | ~9.0 GB | **Sangat Direkomendasikan** (All-around, coding, bahasa Indonesia, context 8k-32k, multi-user simultan) |
| `hugging-quants/Meta-Llama-3.1-8B-Instruct-AWQ-INT4` | ~6.0 GB | ~8.5 GB | Penalaran umum, percakapan bahasa Inggris/Indonesia |
| `deepseek-ai/DeepSeek-R1-Distill-Qwen-7B` (AWQ) | ~5.5 GB | ~9.0 GB | Penalaran matematika, logika, dan coding mendalam (*Reasoning Model*) |
| `Qwen/Qwen2.5-14B-Instruct-AWQ` | ~10.0 GB | ~4.5 GB | Kapasitas penalaran lebih tinggi (cocok untuk 1-4 user concurrency) |

---

## 3. Cara Mengunduh Model ke Folder Ini

### Metode A: Menggunakan `huggingface-cli` (Paling Cepat & Andal)

Buka terminal PowerShell atau CMD di folder root proyek, lalu jalankan:

```powershell
# 1. Instal library downloader Hugging Face jika belum ada
pip install huggingface_hub

# 2. Unduh model langsung ke dalam folder ./models/<NAMA_FOLDER>
huggingface-cli download Qwen/Qwen2.5-7B-Instruct-AWQ --local-dir ./models/Qwen2.5-7B-Instruct-AWQ

# Contoh lain jika ingin mengunduh Llama-3.1 8B AWQ:
huggingface-cli download hugging-quants/Meta-Llama-3.1-8B-Instruct-AWQ-INT4 --local-dir ./models/Meta-Llama-3.1-8B-Instruct-AWQ
```

*(Catatan: Jika mengunduh model berlisensi seperti Llama-3 resmi, login terlebih dahulu dengan perintah `huggingface-cli login`)*

### Metode B: Unduh Manual via Browser

1. Kunjungi halaman Hugging Face model (misal: `https://huggingface.co/Qwen/Qwen2.5-7B-Instruct-AWQ/tree/main`).
2. Buat folder baru di dalam `./models/` dengan nama yang sama.
3. Unduh seluruh file (`config.json`, file `*.safetensors`, dan file tokenizer `*.json`) lalu pindahkan ke folder tersebut.

---

## 4. Cara Menjalankan

Setelah file model selesai diunduh dan tersusun di dalam `./models/<NAMA_MODEL>`, Anda cukup menjalankan:
```cmd
start.bat
```
Script `start.bat` akan secara otomatis mendeteksi model Anda, mengonfigurasi kuantisasi AWQ, dan menyalakan engine vLLM!

