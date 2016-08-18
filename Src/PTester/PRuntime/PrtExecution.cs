﻿
using System;
using System.Collections.Generic;
using System.Linq;

namespace P.Runtime
{
    public class PrtCommonFunctions
    {
        public static PrtIgnoreFun IgnoreFun = new PrtIgnoreFun();
        public static PrtSkipFun SkipFun = new PrtSkipFun();
    }

    public class PrtIgnoreFun : PrtFun
    {
        public override string Name
        {
            get
            {
                return "Ignore";
            }
        }

        public override bool IsAnonFun
        {
            get
            {
                return true;
            }
        }

        public override void Execute(StateImpl application, PrtMachine parent)
        {
            throw new NotImplementedException();
        }

        public override List<PrtValue> CreateLocals(params PrtValue[] args)
        {
            throw new NotImplementedException();
        }
    }

    public class PrtSkipFun : PrtFun
    {
        public override string Name
        {
            get
            {
                return "Skip";
            }
        }

        public override bool IsAnonFun
        {
            get
            {
                return true;
            }
        }

        public override void Execute(StateImpl application, PrtMachine parent)
        {
            throw new NotImplementedException();
        }

        public override List<PrtValue> CreateLocals(params PrtValue[] args)
        {
            throw new NotImplementedException();
        }
    }

    public abstract class PrtFun
    {
        public abstract string Name
        {
            get;
        }

        public abstract bool IsAnonFun
        {
            get;
        } 

        public List<Dictionary<PrtEvent, PrtFun>> receiveCases;
        
        public PrtFun()
        {
            receiveCases = new List<Dictionary<PrtEvent, PrtFun>>();
        }

        public abstract List<PrtValue> CreateLocals(params PrtValue[] args);

        public abstract void Execute(StateImpl application, PrtMachine parent);
    }

    public class PrtEvent
    {
        public static int DefaultMaxInstances = int.MaxValue;
        public static PrtEvent NullEvent = null;
        public static PrtEvent HaltEvent = new PrtEvent("Halt", new PrtNullType(), DefaultMaxInstances, false);
        

        public string name;
        public PrtType payloadType;
        public int maxInstances;
        public bool doAssume;

        public PrtEvent(string name, PrtType payload, int mInstances, bool doAssume)
        {
            this.name = name;
            this.payloadType = payload;
            this.maxInstances = mInstances;
            this.doAssume = doAssume;
        }
    };

    public class PrtTransition
    {
        public PrtFun transitionFun; // isPush <==> fun == null
        public PrtState gotoState;
        public bool isPushTran;
        public PrtTransition(PrtFun fun, PrtState toState, bool isPush)
        {
            this.transitionFun = fun;
            this.gotoState = toState;
            this.isPushTran = isPush;

        }
    };

    public enum StateTemperature
    {
        Cold,
        Warm,
        Hot
    };

    public class PrtState
    {
        public string name;
        public PrtFun entryFun;
        public PrtFun exitFun;
        public Dictionary<PrtEvent, PrtTransition> transitions;
        public Dictionary<PrtEvent, PrtFun> dos;
        public bool hasNullTransition;
        public StateTemperature temperature;
        public HashSet<PrtEvent> deferredSet;

        public PrtState(string name, PrtFun entryFun, PrtFun exitFun, bool hasNullTransition, StateTemperature temperature)
        {
            this.name = name;
            this.entryFun = entryFun;
            this.exitFun = exitFun;
            this.transitions = new Dictionary<PrtEvent, PrtTransition>();
            this.dos = new Dictionary<PrtEvent, PrtFun>();
            this.hasNullTransition = hasNullTransition;
            this.temperature = temperature;
        }
    };

    public class PrtEventNode
    {
        public PrtEvent ev;
        public PrtValue arg;

        public PrtEventNode(PrtEvent e, PrtValue payload)
        {
            ev = e;
            arg = payload.Clone();
        }

        public PrtEventNode Clone()
        {
            return new PrtEventNode(this.ev, this.arg);
        }
    }

    public class PrtEventBuffer
    {
        List<PrtEventNode> events;
        public PrtEventBuffer()
        {
            events = new List<PrtEventNode>();
        }

        public PrtEventBuffer Clone()
        {
            var clonedVal = new PrtEventBuffer();
            foreach(var ev in this.events)
            {
                clonedVal.events.Add(ev.Clone());
            }
            return clonedVal;
        }
        public int Size()
        {
            return events.Count();
        }
        public int CalculateInstances(PrtEvent e)
        {
            return events.Select(en => en.ev).Where(ev => ev == e).Count();
        }

        public void EnqueueEvent(PrtEvent e, PrtValue arg)
        {
            if (e.maxInstances == PrtEvent.DefaultMaxInstances)
            {
                events.Add(new PrtEventNode(e, arg));
            }
            else
            {
                if (CalculateInstances(e) == e.maxInstances)
                {
                    if (e.doAssume)
                    {
                        throw new PrtAssumeFailureException();
                    }
                    else
                    {
                        throw new PrtMaxEventInstancesExceededException(
                            String.Format(@"< Exception > Attempting to enqueue event {0} more than max instance of {1}\n", e.name, e.maxInstances));
                    }
                }
                else
                {
                    events.Add(new PrtEventNode(e, arg));
                }
            }
        }

        public void DequeueEvent(PrtMachine owner)
        {
            HashSet<PrtEvent> deferredSet;
            HashSet<PrtEvent> receiveSet;

            deferredSet = owner.CurrentState.deferredSet;
            receiveSet = owner.receiveSet;

            int iter = 0;
            while (iter < events.Count)
            { 
                if ((receiveSet.Count == 0 && !deferredSet.Contains(events[iter].ev))
                    || (receiveSet.Count > 0 && receiveSet.Contains(events[iter].ev)))
                {
                    owner.currentTrigger = events[iter].ev;
                    owner.currentPayload = events[iter].arg;
                    events.Remove(events[iter]);
                    return;
                }
                else
                {
                    continue;
                }
            }
        }

        public bool IsEnabled(PrtMachine owner)
        {
            HashSet<PrtEvent> deferredSet;
            HashSet<PrtEvent> receiveSet;

            deferredSet = owner.CurrentState.deferredSet;
            receiveSet = owner.receiveSet;
            foreach (var evNode in events)
            {
                if ((receiveSet.Count == 0 && !deferredSet.Contains(evNode.ev))
                    || (receiveSet.Count > 0 && receiveSet.Contains(evNode.ev)))
                {
                    return true;
                }

            }
            return false;
        }
    }

    internal class PrtStateStackFrame
    {
        public PrtState state;
        public HashSet<PrtEvent> deferredSet;
        public HashSet<PrtEvent> actionSet;

        public PrtStateStackFrame(PrtState st, HashSet<PrtEvent> defSet, HashSet<PrtEvent> actSet)
        {
            this.state = st;
            this.deferredSet = new HashSet<PrtEvent>();
            foreach (var item in defSet)
                this.deferredSet.Add(item);

            this.actionSet = new HashSet<PrtEvent>();
            foreach (var item in actSet)
                this.actionSet.Add(item);
        }

        public PrtStateStackFrame Clone()
        {
            return new PrtStateStackFrame(state, deferredSet, actionSet);
        }

    }
    
    public class PrtStateStack
    {
        public PrtStateStack()
        {
            stateStack = new Stack<PrtStateStackFrame>();
        }

        private Stack<PrtStateStackFrame> stateStack;

        public PrtStateStackFrame TopOfStack
        {
            get
            {
                if (stateStack.Count > 0)
                    return stateStack.Peek();
                else
                    return null;
            }
        }
       
        public PrtStateStack Clone()
        {
            var clone = new PrtStateStack();
            foreach(var s in stateStack)
            {
                clone.stateStack.Push(s.Clone());
            }
            clone.stateStack.Reverse();
            return clone;
        }

        public void PopStackFrame()
        {
            stateStack.Pop();
        }


        public void PushStackFrame(PrtState state)
        {
            var deferredSet = new HashSet<PrtEvent>();
            if (TopOfStack != null)
            {
                deferredSet.UnionWith(TopOfStack.deferredSet);
            }
            deferredSet.UnionWith(state.deferredSet);
            deferredSet.ExceptWith(state.dos.Keys);
            deferredSet.ExceptWith(state.transitions.Keys);

            var actionSet = new HashSet<PrtEvent>();
            if (TopOfStack != null)
            {
                actionSet.UnionWith(TopOfStack.actionSet);
            }
            actionSet.ExceptWith(state.deferredSet);
            actionSet.UnionWith(state.dos.Keys);
            actionSet.ExceptWith(state.transitions.Keys);

            //push the new state on stack
            stateStack.Push(new PrtStateStackFrame(state, deferredSet, actionSet));
        }

        public bool HasNullTransitionOrAction()
        {
            if (TopOfStack.state.hasNullTransition) return true;
            return TopOfStack.actionSet.Contains(PrtEvent.NullEvent);
        }
    }


    #region Function Stack
    public enum PrtContinuationReason : int
    {
        Return,
        Nondet,
        Pop,
        Raise,
        Receive,
        Send,
        NewMachine,
        Goto
    };

    public class PrtFunStackFrame
    {
        public int returnTolocation;
        public List<PrtValue> locals;
        
        public PrtFun fun;
        public PrtFunStackFrame(PrtFun fun,  List<PrtValue> locs)
        {
            this.fun = fun;
            this.locals = new List<PrtValue>();
            foreach(var l in locs)
            {
                locals.Add(l.Clone());
            }
            returnTolocation = 0;
        }

        public PrtFunStackFrame(PrtFun fun, List<PrtValue> locs, int retLocation)
        {
            this.fun = fun;
            this.locals = new List<PrtValue>();
            foreach (var l in locs)
            {
                locals.Add(l.Clone());
            }
            returnTolocation = retLocation;
        }

        public PrtFunStackFrame Clone()
        {
            return new PrtFunStackFrame(this.fun, this.locals, this.returnTolocation);
        }
    }

    public class PrtFunStack
    {
        private Stack<PrtFunStackFrame> funStack;
        public PrtFunStack()
        {
            funStack = new Stack<PrtFunStackFrame>();
        }

        public PrtFunStack Clone()
        {
            var clonedStack = new PrtFunStack();
            foreach(var frame in funStack)
            {
                clonedStack.funStack.Push(frame.Clone());
            }
            clonedStack.funStack.Reverse();
            return clonedStack;
        }

        public PrtFunStackFrame TopOfStack
        {
            get
            {
                if (funStack.Count == 0)
                    return null;
                else
                    return funStack.Peek();
            }
        }

        public void PushFun(PrtFun fun, List<PrtValue> locals)
        {
            funStack.Push(new PrtFunStackFrame(fun, locals));
        }

        public void PushFun(PrtFun fun, List<PrtValue> locals, int retLoc)
        {
            funStack.Push(new PrtFunStackFrame(fun, locals, retLoc));
        }

        public PrtFunStackFrame PopFun()
        {
            return funStack.Pop();
        }

        

    }

    public class PrtContinuation
    {
        
        public PrtContinuationReason reason;
        public PrtMachine createdMachine;
        public int receiveIndex;
        public PrtValue retVal;
        public List<PrtValue> retLocals;
        // The nondet field is different from the fields above because it is used 
        // by ReentrancyHelper to pass the choice to the nondet choice point.
        // Therefore, nondet should not be reinitialized in this class.
        public bool nondet;

        public PrtContinuation()
        {
            reason = PrtContinuationReason.Return;
            createdMachine = null;
            retVal = null;
            nondet = false;
            retLocals = new List<PrtValue>();
            receiveIndex = -1;
        }

        public PrtContinuation Clone()
        {
            var clonedVal = new PrtContinuation();
            clonedVal.reason = this.reason;
            clonedVal.createdMachine = this.createdMachine;
            clonedVal.receiveIndex = this.receiveIndex;
            clonedVal.retVal = this.retVal.Clone();
            foreach(var loc in retLocals)
            {
                clonedVal.retLocals.Add(loc.Clone());
            }

            return clonedVal;
        }
    }

    #endregion
}