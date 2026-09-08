# PDF-form-to-APR open-weight VLM benchmark

11 real government forms (5 federal, 6 Connecticut) x 7 open-weight models, all running locally via MLX. Field matching is fuzzy-label similarity (threshold 0.55) against hand-verified ground truth, not exact string match.

## Aggregate results

| Model | Valid .aprt | Precision | Recall | F1 | Type agreement | Avg time/form |
|---|---:|---:|---:|---:|---:|---:|
| IBM Granite-Docling 258M | 100% | 0.17 | 0.34 | 0.18 | 0.86 | 50.7s |
| Qwen3-VL-8B-Instruct (4-bit) | 100% | 0.59 | 0.82 | 0.66 | 0.87 | 255.0s |
| dots.ocr (converted MLX 4-bit) | 82% | 0.13 | 0.09 | 0.11 | 0.71 | 147.9s |
| Florence-2-base-ft (4-bit) | 100% | 0.19 | 0.28 | 0.20 | 0.75 | 10.8s |
| Qwen3-VL-4B-Instruct (4-bit) | 100% | 0.67 | 0.83 | 0.71 | 0.90 | 106.1s |
| InternVL3-8B (MLX 4-bit) | 100% | 0.52 | 0.78 | 0.57 | 0.78 | 219.9s |
| PaliGemma 2 3B mix (448, 4-bit) | 100% | 0.43 | 0.54 | 0.45 | 0.72 | 77.8s |

## Per-form detail

| Form | Model | GT fields | Pred fields | Matched | Precision | Recall | F1 | Valid |
|---|---|---:|---:|---:|---:|---:|---:|---:|
| IRS Form W-9 (Request for Taxpayer Identification Number) | IBM Granite-Docling 258M | 16 | 8 | 0 | 0.00 | 0.00 | 0.00 | ✓ |
| IRS Form W-9 (Request for Taxpayer Identification Number) | Qwen3-VL-8B-Instruct (4-bit) | 16 | 45 | 12 | 0.27 | 0.75 | 0.39 | ✓ |
| IRS Form W-9 (Request for Taxpayer Identification Number) | dots.ocr (converted MLX 4-bit) | 16 | 1 | 0 | 0.00 | 0.00 | 0.00 | ✓ |
| IRS Form W-9 (Request for Taxpayer Identification Number) | Florence-2-base-ft (4-bit) | 16 | 59 | 2 | 0.03 | 0.12 | 0.05 | ✓ |
| IRS Form W-9 (Request for Taxpayer Identification Number) | Qwen3-VL-4B-Instruct (4-bit) | 16 | 28 | 12 | 0.43 | 0.75 | 0.55 | ✓ |
| IRS Form W-9 (Request for Taxpayer Identification Number) | InternVL3-8B (MLX 4-bit) | 16 | 149 | 14 | 0.09 | 0.88 | 0.17 | ✓ |
| IRS Form W-9 (Request for Taxpayer Identification Number) | PaliGemma 2 3B mix (448, 4-bit) | 16 | 64 | 9 | 0.14 | 0.56 | 0.23 | ✓ |
| IRS Form W-4 (Employee's Withholding Certificate) | IBM Granite-Docling 258M | 19 | 130 | 0 | 0.00 | 0.00 | 0.00 | ✓ |
| IRS Form W-4 (Employee's Withholding Certificate) | Qwen3-VL-8B-Instruct (4-bit) | 19 | 43 | 12 | 0.28 | 0.63 | 0.39 | ✓ |
| IRS Form W-4 (Employee's Withholding Certificate) | dots.ocr (converted MLX 4-bit) | 19 | 3 | 0 | 0.00 | 0.00 | 0.00 | ✓ |
| IRS Form W-4 (Employee's Withholding Certificate) | Florence-2-base-ft (4-bit) | 19 | 120 | 2 | 0.02 | 0.10 | 0.03 | ✓ |
| IRS Form W-4 (Employee's Withholding Certificate) | Qwen3-VL-4B-Instruct (4-bit) | 19 | 54 | 16 | 0.30 | 0.84 | 0.44 | ✓ |
| IRS Form W-4 (Employee's Withholding Certificate) | InternVL3-8B (MLX 4-bit) | 19 | 84 | 15 | 0.18 | 0.79 | 0.29 | ✓ |
| IRS Form W-4 (Employee's Withholding Certificate) | PaliGemma 2 3B mix (448, 4-bit) | 19 | 53 | 10 | 0.19 | 0.53 | 0.28 | ✓ |
| IRS Form SS-4 (Application for Employer Identification Number) | IBM Granite-Docling 258M | 52 | 177 | 0 | 0.00 | 0.00 | 0.00 | ✓ |
| IRS Form SS-4 (Application for Employer Identification Number) | Qwen3-VL-8B-Instruct (4-bit) | 52 | 41 | 33 | 0.81 | 0.64 | 0.71 | ✓ |
| IRS Form SS-4 (Application for Employer Identification Number) | dots.ocr (converted MLX 4-bit) | 52 | 2 | 0 | 0.00 | 0.00 | 0.00 | ✓ |
| IRS Form SS-4 (Application for Employer Identification Number) | Florence-2-base-ft (4-bit) | 52 | 33 | 8 | 0.24 | 0.15 | 0.19 | ✓ |
| IRS Form SS-4 (Application for Employer Identification Number) | Qwen3-VL-4B-Instruct (4-bit) | 52 | 36 | 30 | 0.83 | 0.58 | 0.68 | ✓ |
| IRS Form SS-4 (Application for Employer Identification Number) | InternVL3-8B (MLX 4-bit) | 52 | 35 | 29 | 0.83 | 0.56 | 0.67 | ✓ |
| IRS Form SS-4 (Application for Employer Identification Number) | PaliGemma 2 3B mix (448, 4-bit) | 52 | 33 | 13 | 0.39 | 0.25 | 0.31 | ✓ |
| IRS Form 8822 (Change of Address) | IBM Granite-Docling 258M | 31 | 5 | 1 | 0.20 | 0.03 | 0.06 | ✓ |
| IRS Form 8822 (Change of Address) | Qwen3-VL-8B-Instruct (4-bit) | 31 | 57 | 28 | 0.49 | 0.90 | 0.64 | ✓ |
| IRS Form 8822 (Change of Address) | dots.ocr (converted MLX 4-bit) | 31 | 0 | 0 | 0.00 | 0.00 | 0.00 | ✗ |
| IRS Form 8822 (Change of Address) | Florence-2-base-ft (4-bit) | 31 | 13 | 0 | 0.00 | 0.00 | 0.00 | ✓ |
| IRS Form 8822 (Change of Address) | Qwen3-VL-4B-Instruct (4-bit) | 31 | 37 | 30 | 0.81 | 0.97 | 0.88 | ✓ |
| IRS Form 8822 (Change of Address) | InternVL3-8B (MLX 4-bit) | 31 | 35 | 24 | 0.69 | 0.77 | 0.73 | ✓ |
| IRS Form 8822 (Change of Address) | PaliGemma 2 3B mix (448, 4-bit) | 31 | 26 | 20 | 0.77 | 0.65 | 0.70 | ✓ |
| USCIS Form I-9 (Employment Eligibility Verification) | IBM Granite-Docling 258M | 44 | 198 | 36 | 0.18 | 0.82 | 0.30 | ✓ |
| USCIS Form I-9 (Employment Eligibility Verification) | Qwen3-VL-8B-Instruct (4-bit) | 44 | 99 | 41 | 0.41 | 0.93 | 0.57 | ✓ |
| USCIS Form I-9 (Employment Eligibility Verification) | dots.ocr (converted MLX 4-bit) | 44 | 4 | 0 | 0.00 | 0.00 | 0.00 | ✓ |
| USCIS Form I-9 (Employment Eligibility Verification) | Florence-2-base-ft (4-bit) | 44 | 106 | 32 | 0.30 | 0.73 | 0.43 | ✓ |
| USCIS Form I-9 (Employment Eligibility Verification) | Qwen3-VL-4B-Instruct (4-bit) | 44 | 151 | 42 | 0.28 | 0.95 | 0.43 | ✓ |
| USCIS Form I-9 (Employment Eligibility Verification) | InternVL3-8B (MLX 4-bit) | 44 | 153 | 42 | 0.28 | 0.95 | 0.43 | ✓ |
| USCIS Form I-9 (Employment Eligibility Verification) | PaliGemma 2 3B mix (448, 4-bit) | 44 | 96 | 36 | 0.38 | 0.82 | 0.51 | ✓ |
| Connecticut Form CT-W4 (Employee's Withholding Certificate) | IBM Granite-Docling 258M | 25 | 164 | 16 | 0.10 | 0.64 | 0.17 | ✓ |
| Connecticut Form CT-W4 (Employee's Withholding Certificate) | Qwen3-VL-8B-Instruct (4-bit) | 25 | 27 | 24 | 0.89 | 0.96 | 0.92 | ✓ |
| Connecticut Form CT-W4 (Employee's Withholding Certificate) | dots.ocr (converted MLX 4-bit) | 25 | 10 | 3 | 0.30 | 0.12 | 0.17 | ✓ |
| Connecticut Form CT-W4 (Employee's Withholding Certificate) | Florence-2-base-ft (4-bit) | 25 | 36 | 1 | 0.03 | 0.04 | 0.03 | ✓ |
| Connecticut Form CT-W4 (Employee's Withholding Certificate) | Qwen3-VL-4B-Instruct (4-bit) | 25 | 25 | 25 | 1.00 | 1.00 | 1.00 | ✓ |
| Connecticut Form CT-W4 (Employee's Withholding Certificate) | InternVL3-8B (MLX 4-bit) | 25 | 47 | 20 | 0.43 | 0.80 | 0.56 | ✓ |
| Connecticut Form CT-W4 (Employee's Withholding Certificate) | PaliGemma 2 3B mix (448, 4-bit) | 25 | 26 | 8 | 0.31 | 0.32 | 0.31 | ✓ |
| Connecticut DMV Form J23 (Copy Records Request) | IBM Granite-Docling 258M | 41 | 13 | 3 | 0.23 | 0.07 | 0.11 | ✓ |
| Connecticut DMV Form J23 (Copy Records Request) | Qwen3-VL-8B-Instruct (4-bit) | 41 | 48 | 34 | 0.71 | 0.83 | 0.76 | ✓ |
| Connecticut DMV Form J23 (Copy Records Request) | dots.ocr (converted MLX 4-bit) | 41 | 8 | 0 | 0.00 | 0.00 | 0.00 | ✓ |
| Connecticut DMV Form J23 (Copy Records Request) | Florence-2-base-ft (4-bit) | 41 | 7 | 2 | 0.29 | 0.05 | 0.08 | ✓ |
| Connecticut DMV Form J23 (Copy Records Request) | Qwen3-VL-4B-Instruct (4-bit) | 41 | 49 | 35 | 0.71 | 0.85 | 0.78 | ✓ |
| Connecticut DMV Form J23 (Copy Records Request) | InternVL3-8B (MLX 4-bit) | 41 | 48 | 36 | 0.75 | 0.88 | 0.81 | ✓ |
| Connecticut DMV Form J23 (Copy Records Request) | PaliGemma 2 3B mix (448, 4-bit) | 41 | 49 | 16 | 0.33 | 0.39 | 0.36 | ✓ |
| Connecticut DMV Form B-58 (Change of Address, Individual) | IBM Granite-Docling 258M | 27 | 46 | 14 | 0.30 | 0.52 | 0.38 | ✓ |
| Connecticut DMV Form B-58 (Change of Address, Individual) | Qwen3-VL-8B-Instruct (4-bit) | 27 | 33 | 24 | 0.73 | 0.89 | 0.80 | ✓ |
| Connecticut DMV Form B-58 (Change of Address, Individual) | dots.ocr (converted MLX 4-bit) | 27 | 1 | 0 | 0.00 | 0.00 | 0.00 | ✓ |
| Connecticut DMV Form B-58 (Change of Address, Individual) | Florence-2-base-ft (4-bit) | 27 | 36 | 9 | 0.25 | 0.33 | 0.29 | ✓ |
| Connecticut DMV Form B-58 (Change of Address, Individual) | Qwen3-VL-4B-Instruct (4-bit) | 27 | 33 | 24 | 0.73 | 0.89 | 0.80 | ✓ |
| Connecticut DMV Form B-58 (Change of Address, Individual) | InternVL3-8B (MLX 4-bit) | 27 | 35 | 24 | 0.69 | 0.89 | 0.77 | ✓ |
| Connecticut DMV Form B-58 (Change of Address, Individual) | PaliGemma 2 3B mix (448, 4-bit) | 27 | 29 | 15 | 0.52 | 0.56 | 0.54 | ✓ |
| Connecticut DMV Form A-25 (Request for Hearing to Contest Vehicle Towing) | IBM Granite-Docling 258M | 15 | 18 | 13 | 0.72 | 0.87 | 0.79 | ✓ |
| Connecticut DMV Form A-25 (Request for Hearing to Contest Vehicle Towing) | Qwen3-VL-8B-Instruct (4-bit) | 15 | 16 | 15 | 0.94 | 1.00 | 0.97 | ✓ |
| Connecticut DMV Form A-25 (Request for Hearing to Contest Vehicle Towing) | dots.ocr (converted MLX 4-bit) | 15 | 0 | 0 | 0.00 | 0.00 | 0.00 | ✗ |
| Connecticut DMV Form A-25 (Request for Hearing to Contest Vehicle Towing) | Florence-2-base-ft (4-bit) | 15 | 27 | 14 | 0.52 | 0.93 | 0.67 | ✓ |
| Connecticut DMV Form A-25 (Request for Hearing to Contest Vehicle Towing) | Qwen3-VL-4B-Instruct (4-bit) | 15 | 17 | 15 | 0.88 | 1.00 | 0.94 | ✓ |
| Connecticut DMV Form A-25 (Request for Hearing to Contest Vehicle Towing) | InternVL3-8B (MLX 4-bit) | 15 | 21 | 14 | 0.67 | 0.93 | 0.78 | ✓ |
| Connecticut DMV Form A-25 (Request for Hearing to Contest Vehicle Towing) | PaliGemma 2 3B mix (448, 4-bit) | 15 | 19 | 13 | 0.68 | 0.87 | 0.77 | ✓ |
| Connecticut DMV Form A-83 (Special Power of Attorney) | IBM Granite-Docling 258M | 15 | 124 | 8 | 0.07 | 0.53 | 0.12 | ✓ |
| Connecticut DMV Form A-83 (Special Power of Attorney) | Qwen3-VL-8B-Instruct (4-bit) | 15 | 26 | 9 | 0.35 | 0.60 | 0.44 | ✓ |
| Connecticut DMV Form A-83 (Special Power of Attorney) | dots.ocr (converted MLX 4-bit) | 15 | 9 | 6 | 0.67 | 0.40 | 0.50 | ✓ |
| Connecticut DMV Form A-83 (Special Power of Attorney) | Florence-2-base-ft (4-bit) | 15 | 22 | 9 | 0.41 | 0.60 | 0.49 | ✓ |
| Connecticut DMV Form A-83 (Special Power of Attorney) | Qwen3-VL-4B-Instruct (4-bit) | 15 | 9 | 7 | 0.78 | 0.47 | 0.58 | ✓ |
| Connecticut DMV Form A-83 (Special Power of Attorney) | InternVL3-8B (MLX 4-bit) | 15 | 15 | 7 | 0.47 | 0.47 | 0.47 | ✓ |
| Connecticut DMV Form A-83 (Special Power of Attorney) | PaliGemma 2 3B mix (448, 4-bit) | 15 | 16 | 7 | 0.44 | 0.47 | 0.45 | ✓ |
| Connecticut DMV Form B-225P (Disabled Parking Placard, Renewable) | IBM Granite-Docling 258M | 28 | 118 | 8 | 0.07 | 0.29 | 0.11 | ✓ |
| Connecticut DMV Form B-225P (Disabled Parking Placard, Renewable) | Qwen3-VL-8B-Instruct (4-bit) | 28 | 39 | 24 | 0.61 | 0.86 | 0.72 | ✓ |
| Connecticut DMV Form B-225P (Disabled Parking Placard, Renewable) | dots.ocr (converted MLX 4-bit) | 28 | 29 | 14 | 0.48 | 0.50 | 0.49 | ✓ |
| Connecticut DMV Form B-225P (Disabled Parking Placard, Renewable) | Florence-2-base-ft (4-bit) | 28 | 16 | 0 | 0.00 | 0.00 | 0.00 | ✓ |
| Connecticut DMV Form B-225P (Disabled Parking Placard, Renewable) | Qwen3-VL-4B-Instruct (4-bit) | 28 | 33 | 22 | 0.67 | 0.79 | 0.72 | ✓ |
| Connecticut DMV Form B-225P (Disabled Parking Placard, Renewable) | InternVL3-8B (MLX 4-bit) | 28 | 28 | 18 | 0.64 | 0.64 | 0.64 | ✓ |
| Connecticut DMV Form B-225P (Disabled Parking Placard, Renewable) | PaliGemma 2 3B mix (448, 4-bit) | 28 | 23 | 14 | 0.61 | 0.50 | 0.55 | ✓ |
