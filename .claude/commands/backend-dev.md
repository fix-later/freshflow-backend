---
description: Tạo team backend-dev (leader/coder/reviewer) và chạy quy trình giao việc tự động
---

# Team: backend-dev

Triển khai `TeamCreate` với team name là **backend-dev** với các teammate sau.

## Danh sách team

- **leader**: `.claude/agents/leader.md`
- **coder**: `.claude/agents/coder.md`
- **reviewer**: `.claude/agents/reviewer.md`

## Quy trình làm việc

- Tất cả mọi công việc trao đổi với teammate trong team đều thực hiện qua **SendMessage**, **không tự tạo subagent mới**.
- **Leader** giao việc xuống cho **coder**.
- **Coder** code xong thì đưa kết quả xuống cho **reviewer** để review lại.
- **Reviewer** review đúng với plan thì **pass** và báo với **leader** là task đã hoàn thành; nếu **chưa pass** thì đẩy task lại cho **coder** đến khi nào pass thì thôi.
- Khi **leader** nhận được báo task hoàn thành sẽ kiểm tra xem còn task nào không:
  - Còn → thực hiện tiếp task sau.
  - Không còn → tạm dừng.

## Cách kích hoạt

1. `TeamCreate` với `team_name: backend-dev`.
2. Spawn (đánh thức) 3 teammate qua Agent tool với `team_name: backend-dev` và `name`/`subagent_type` tương ứng: `leader`, `coder`, `reviewer`.
3. Supervisor (phiên chính) chuyển yêu cầu của người dùng cho **leader** qua `SendMessage`; leader điều phối phần còn lại.
