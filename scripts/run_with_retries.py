#!/usr/bin/env python3
import subprocess, sys, os, time, argparse

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--name', required=True)
    parser.add_argument('--timeout', type=int, default=1200)
    parser.add_argument('--retries', type=int, default=5)
    parser.add_argument('command', nargs=argparse.REMAINDER)
    args = parser.parse_args()

    cmd = args.command
    if cmd and cmd[0] == '--':
        cmd = cmd[1:]

    log_dir = 'proof/runtime/logs'
    os.makedirs(log_dir, exist_ok=True)

    backoff = 10
    for attempt in range(1, args.retries + 1):
        log_file = os.path.join(log_dir, f'{args.name}_attempt{attempt}.log')
        print(f'[run_with_retries] {args.name} attempt {attempt}/{args.retries}')
        try:
            result = subprocess.run(
                cmd, timeout=args.timeout,
                capture_output=True, text=True, cwd=os.getcwd()
            )
            with open(log_file, 'w') as f:
                f.write(f'=== {args.name} attempt {attempt} ===\n')
                f.write(f'Command: {" ".join(cmd)}\n')
                f.write(f'Exit code: {result.returncode}\n\n')
                f.write('--- STDOUT ---\n')
                f.write(result.stdout or '')
                f.write('\n--- STDERR ---\n')
                f.write(result.stderr or '')

            if result.returncode == 0:
                print(f'[run_with_retries] {args.name} PASSED on attempt {attempt}')
                print(result.stdout)
                sys.exit(0)
            else:
                print(f'[run_with_retries] {args.name} FAILED (exit {result.returncode})')
                print(result.stdout[-500:] if result.stdout else '')
                print(result.stderr[-500:] if result.stderr else '')
        except subprocess.TimeoutExpired:
            with open(log_file, 'w') as f:
                f.write(f'=== {args.name} attempt {attempt} TIMED OUT after {args.timeout}s ===\n')
            print(f'[run_with_retries] {args.name} TIMED OUT on attempt {attempt}')

        if attempt < args.retries:
            wait = min(backoff, 120)
            print(f'[run_with_retries] Waiting {wait}s before retry...')
            time.sleep(wait)
            backoff *= 2

    print(f'[run_with_retries] {args.name} FAILED after {args.retries} attempts')
    sys.exit(1)

if __name__ == '__main__':
    main()
