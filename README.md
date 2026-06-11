# Adaptive Boss Difficulty Tuning via Reinforcement Learning Personas
### Automated QA Framework for Target Difficulty Tracking in Multi-User Game Environments

---
## Can reinforcement learning reduce the cost of repetitive game QA?
##### This project presents an automated QA framework that tunes boss difficulty toward designer-defined target clear rates using RL-based player personas.
---

## TL;DR

* Built an automated QA framework that tunes boss difficulty toward a designer-defined target clear rate.
* Trained reinforcement learning player agents using PPO in Unity ML-Agents.
* Generated human-like personas with different skill levels through cognitive masking and stochastic execution errors.
* Compared linear and Gaussian reward formulations for boss optimization.
* Achieved a **48% clear rate against a target difficulty of 50%**, resulting in only **2% absolute error**.

---

## Problem

Balancing boss difficulty is one of the most repetitive and expensive tasks in game QA.

Designers typically adjust boss parameters manually, conduct repeated playtests, collect clear rates, and iterate until the intended difficulty is achieved.

This process becomes increasingly costly in multiplayer environments where player skill distributions vary significantly.

This project proposes an automated QA framework that leverages reinforcement learning personas to tune boss parameters toward a designer-intended target clear rate.

---

## System Overview

Designer Target Difficulty (e.g., 50% clear rate)

↓

PPO-based Player Agent Training

↓

Persona Generation

(Cognitive Masking + Stochastic Execution Errors)

↓

100 Automated Battle Simulations

↓

Boss Parameter Optimization

(Pattern Cooldowns + Pattern Weights)

↓

Target Difficulty Tracking

---

## Persona Generation

A player agent was first trained using PPO to evade predefined boss attack patterns.

To simulate realistic player populations, additional constraints were introduced:

* Cognitive masking
* Probabilistic execution mistakes

These mechanisms generated multiple personas with different skill levels.

### Persona Performance

| Persona      | Expected Avoidance Rate |
| ------------ | ----------------------: |
| Expert       |                     91% |
| Intermediate |                     74% |
| Novice       |                     69% |

Unlike conventional evaluations using a single optimal agent, this approach reflects the diversity of real player skill distributions.

---

## Boss Optimization

The boss agent was trained to automatically adjust its parameters in order to achieve a designer-specified target difficulty.

Optimizable parameters included:

* Boss pattern cooldowns
* Boss pattern selection weights

For each optimization step:

1. Personas with randomized DPS values participated in battle simulations.
2. 100 automated encounters were executed.
3. The observed clear rate was compared against the target difficulty.
4. Rewards were assigned based on the difference.
5. Boss parameters were updated accordingly.

---

## Reward Function Comparison

Two reward formulations were investigated.

### Linear Reward

Reward was assigned proportionally to the distance between observed and target clear rates.

Result:

* Large oscillations.
* Unstable convergence behavior.

### Gaussian Reward

Higher rewards were concentrated.

Result:
* More accurate target tracking.

---

## Final Evaluation

After optimization, 40 candidate boss configurations were evaluated.

The configuration closest to the intended difficulty was selected and tested against a population of 100 persona agents.

### Results

| Metric              | Value |
| ------------------- | ----: |
| Target Clear Rate   |   50% |
| Observed Clear Rate |   48% |
| Absolute Error      |    2% |

The proposed framework successfully reproduced the designer-intended difficulty with high accuracy.

---

## Technical Stack

* Unity (2D)
* Unity ML-Agents
* PPO (Proximal Policy Optimization)
* C#
* Automated Simulation Pipeline
* Unity Console Log-based Evaluation

---

## Key Contributions

* Proposed an automated QA framework for boss difficulty tuning.
* Introduced realistic player personas using cognitive masking and stochastic execution errors.
* Compared reward formulations for target difficulty tracking.
* Demonstrated accurate difficulty reproduction within a 2% error margin.
* Reduced reliance on repetitive manual playtesting.

---

## Future Work

Potential extensions include:

* Self-play based adaptive boss strategies
* Multi-agent cooperation scenarios
* PyTorch-based custom PPO implementations
* Dynamic difficulty adaptation during live gameplay
