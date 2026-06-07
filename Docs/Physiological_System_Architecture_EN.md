# Physiological System Architecture Design Specification (English Version)

This document defines the architectural principles and specification for the physiological and pathological simulation system in the **Emergency Expanded** mod. It is designed to prevent "vicious cycles" (infinite feedback loops/circular dependencies) while maintaining a high-fidelity medical simulation.

---

## 1. The Core Problem: Vicious Cycles

In medical simulations, a common pitfall is direct coupling between capacity deficiencies and pathological accumulation. For example:
$$\text{Perfusion Deficit} \longrightarrow \text{Acidosis Accumulates} \longrightarrow \text{Direct Reduction of Cardiac Pumping} \longrightarrow \text{Worse Perfusion}$$
This causes the patient's vitals to collapse instantly (within seconds) and blocks resuscitation. Even if blood volume is restored via transfusion, the lowered pumping capacity prevents recovery, causing a logic deadlock.

---

## 2. Layered Unidirectional Physiology Model

To resolve this, the physiological system is strictly segregated into four sequential layers. Information and state updates flow **downward**; upward feedback is restricted to asynchronous, delayed thresholds.

```
+-----------------------------------------------------------+
| Layer 1: Primary Insults                                  |
| - Wounds, Massive Bleeding, Pneumothorax, Burns, MI/VFib  |
+-----------------------------+-----------------------------+
                              | (Direct physical impact)
                              v
+-----------------------------------------------------------+
| Layer 2: Base Capacities                                  |
| - Blood Pumping (Hearts), Breathing (Lungs), Blood Volume  |
+-----------------------------+-----------------------------+
                              | (Deficits generate debt)
                              v
+-----------------------------------------------------------+
| Layer 3: Pathophysiology & Debt                           |
| - Shock (Pressure), Metabolic Acidosis (pH), SIRS         |
+-----------------------------+-----------------------------+
                              | (Sustained debt ruins tissue)
                              v
+-----------------------------------------------------------+
| Layer 4: Terminal Organ Failure                           |
| - MODS (Organ failure), Brain Hypoxia, Death Timer        |
+-----------------------------------------------------------+
                              |
                              | (Asynchronous capability cap / Asynchronous Feedback)
                              + - - - - - - - - - - - - - - - > [Back to Layer 2]
```

### Layer 1: Primary Insults
*   **Definition**: External trauma or physical accidents that act as the raw input to the physiological system.
*   **Examples**: Gunshot wounds, massive bleeding, bone fractures, burns, tension pneumothorax, coronary artery blockage (MI/VFib).

### Layer 2: Base Capacities
*   **Definition**: The base physiological values calculated purely from physical part integrity and blood volume.
*   **Examples**: Blood Pumping (pumping level), Breathing (respiration level), Blood Volume.
*   **Rule**: These capacities are **only** modified by physical part missing/damaged states (Layer 1). They **must not** be directly capped by Layer 3 debt states (e.g., Acidosis/Shock via setMax or direct multipliers).

### Layer 3: Pathophysiology & Debt
*   **Definition**: Cumulative variables representing physiological deficits or metabolic debts.
*   **Examples**: Shock severity, Metabolic Acidosis (pH), SIRS severity.
*   **Rule**: Deficits in Layer 2 (e.g., low pumping, low blood volume) accumulate debt in Layer 3 over time. Restoration of Layer 2 clears Layer 3 debt. **No direct capacity overrides (`setMax`) are allowed in this layer.**

### Layer 4: Terminal Organ Failure
*   **Definition**: Organ damage and irreversible collapse resulting from sustained Layer 3 debts.
*   **Examples**: Multiple Organ Dysfunction Syndrome (MODS), Cerebral Hypoxia, Biological Death Timer.
*   **Rule**: **Only states in Layer 4 are allowed to feedback and cap Layer 2 capacities** (e.g. MODS limiting pumping to 20%). Because it takes a long time for Layer 3 debt to trigger Layer 4 organ failure, this ensures a wide "golden window" for players to intervene.

---

## 3. Mathematical Formula: Physiological Debt

To prevent instantaneous collapse, replace direct capacity reductions with rate-based accumulation (integrals) over time.

### Vicious Cycle Logic (Deprecated):
$$\text{Acidosis.Severity} \ge 0.5 \implies \text{Pumping.setMax} = 0.5$$

### Unidirectional Debt Logic (Required):
1. **Acidosis Rate of Change**:
   $$\Delta\text{Acidosis} = (\text{Perfusion Deficit} - \text{Compensation Threshold}) \times K$$
   where $\text{Perfusion Deficit} = 1.0f - \text{Pumping}$.
2. **Acidosis Recovery**:
   When $\text{Pumping} \ge 0.8f$ and no shock pressure exists, Acidosis decays at a constant rate per day:
   $$\Delta\text{Acidosis} = -\text{DecayRatePerDay} / 1000f$$
3. **Organ Failure (MODS) / Tissue Damage**:
   $$\Delta\text{MODS.Severity} = (\text{Shock.Severity} \times 0.5 + \text{Acidosis.Severity} \times 0.5) \times \text{DamageRate}$$
4. **Organ Failure Effect**:
   Only `MODS.Severity` applies maximum capacity limits (e.g., setMax 0.2 at terminal stage).

---

## 4. Ticking Execution Sequence

To eliminate race conditions and floating updates, all physiological states must be processed in a **single-source-of-truth scheduler** inside `CompEE_PawnGizmos.CompTick` every 60 ticks. The update order is strict:

```csharp
public static void RunUnifiedPhysiologyTick(Pawn pawn)
{
    // 1. Data Collection
    // Calculate pumping/breathing purely based on physical parts and blood volume. No shock/acidosis multipliers here!
    
    // 2. Debt Accumulation
    // Update Shock, Metabolic Acidosis (pH), and SIRS severity based on perfusion and trauma load.
    
    // 3. Organ Damage
    // Update MODS and Cerebral Hypoxia based on sustained shock, acidosis, or lack of perfusion.
    
    // 4. Capacity Constraints
    // Apply setMax capacity limits derived from Layer 4 organ failure states.
}
```

By following these rules, Emergency Expanded maintains high realism while guaranteeing robust, deadlock-free medical gameplay that respects active player rescue efforts.
